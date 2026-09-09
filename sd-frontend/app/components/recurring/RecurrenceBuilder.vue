<template>
  <div class="space-y-4">
    <!-- Preset chips -->
    <div class="flex flex-wrap items-center gap-2">
      <UButton
        v-for="preset in presetOptions"
        :key="preset.key"
        size="sm"
        :variant="selectedPreset === preset.key ? 'solid' : 'outline'"
        :color="selectedPreset === preset.key ? 'primary' : 'neutral'"
        :label="$t(preset.labelKey)"
        @click="applyPreset(preset.key)"
      />
    </div>

    <!-- Custom: explicit mode selection -->
    <UFormField
      v-if="selectedPreset === 'custom'"
      :label="$t('recurring.repeatMode')"
    >
      <USelect
        v-model="customMode"
        :items="modeOptions"
        class="w-full sm:w-64"
        size="lg"
      />
    </UFormField>

    <!-- Weekly: weekday toggle pills -->
    <div v-if="showWeekdayPills">
      <p class="text-sm font-medium text-muted mb-2">
        {{ $t('recurring.repeatOn') }}
      </p>
      <div class="flex flex-wrap gap-1.5">
        <UButton
          v-for="day in weekdayOptions"
          :key="day.bit"
          size="sm"
          :variant="isWeekdaySelected(day.bit) ? 'solid' : 'outline'"
          :color="isWeekdaySelected(day.bit) ? 'primary' : 'neutral'"
          :label="$t(day.labelKey)"
          :aria-pressed="isWeekdaySelected(day.bit)"
          @click="toggleWeekday(day.bit)"
        />
      </div>
    </div>

    <!-- Every N weeks/months: interval stepper -->
    <UFormField
      v-if="showInterval"
      :label="$t('recurring.repeatEvery')"
    >
      <div class="flex items-center gap-2">
        <UInputNumber
          :model-value="spec.interval ?? undefined"
          :min="1"
          :max="52"
          class="w-28"
          @update:model-value="value => update({ interval: toIntOrFallback(value, spec.interval, 1) })"
        />
        <span class="text-sm text-muted">
          {{ activeMode === 4 ? $t('recurring.months') : $t('recurring.weeks') }}
        </span>
      </div>
    </UFormField>

    <!-- Anchor + end date -->
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <UFormField
        :label="$t('recurring.anchorDate')"
        required
      >
        <UiInputDate
          :model-value="spec.anchorDate"
          size="lg"
          @update:model-value="value => update({ anchorDate: value ?? null })"
        />
      </UFormField>
      <UFormField :label="$t('recurring.endDateOptional')">
        <div class="flex items-center gap-2">
          <UiInputDate
            :model-value="spec.endDate"
            size="lg"
            class="flex-1"
            @update:model-value="value => update({ endDate: value ?? null })"
          />
          <UButton
            v-if="spec.endDate"
            icon="i-lucide-x"
            variant="ghost"
            color="neutral"
            size="sm"
            square
            :aria-label="$t('common.close')"
            @click="update({ endDate: null })"
          />
        </div>
      </UFormField>
    </div>

    <!-- Live summary + next occurrences -->
    <div class="rounded-md bg-muted/30 p-3 sm:p-4 space-y-2">
      <div
        v-if="previewError"
        class="flex items-center gap-2 text-sm text-warning"
        role="alert"
      >
        <UIcon
          name="i-lucide-alert-triangle"
          class="size-4 shrink-0"
        />
        {{ $t('recurring.previewError') }}
      </div>
      <template v-else-if="preview">
        <div class="flex items-start gap-2">
          <UIcon
            name="i-lucide-repeat"
            class="size-4 text-primary mt-0.5 shrink-0"
          />
          <p class="text-sm font-medium text-highlighted">
            {{ preview.summary }}
          </p>
        </div>
        <div>
          <p class="text-xs text-dimmed mb-1">
            {{ $t('recurring.previewTitle') }}
          </p>
          <ul class="space-y-1">
            <li
              v-for="occ in preview.nextOccurrences.slice(0, 3)"
              :key="occ"
              class="flex items-center gap-2 text-sm text-muted"
            >
              <UIcon
                name="i-lucide-calendar"
                class="size-3.5 text-dimmed shrink-0"
              />
              {{ formatDateString(occ) }}
            </li>
          </ul>
        </div>
      </template>
      <p
        v-else
        class="text-sm text-dimmed"
      >
        {{ $t('recurring.previewLoading') }}
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { RecurrencePreviewRequest, RecurrencePreviewResponse } from '~/types/domain'
import { useDebounceFn } from '@vueuse/core'

export interface RecurrenceSpecModel {
  recurrenceMode: number
  weekdays: number
  dayOfMonth: number | null
  interval: number | null
  anchorDate: string | null
  endDate: string | null
}

const RECURRENCE_MODE = {
  WEEKLY: 1,
  MONTHLY: 2,
  EVERY_N_WEEKS: 3,
  EVERY_N_MONTHS: 4,
} as const

const props = defineProps<{
  groupId: string
}>()

const spec = defineModel<RecurrenceSpecModel>({ required: true })

const { t } = useI18n()
const { previewOccurrences } = useRecurringExpenses(props.groupId)

type PresetKey = 'weekly' | 'everyTwoWeeks' | 'monthly' | 'custom'

const presetOptions: Array<{ key: PresetKey, labelKey: string }> = [
  { key: 'weekly', labelKey: 'recurring.presetWeekly' },
  { key: 'everyTwoWeeks', labelKey: 'recurring.presetEveryTwoWeeks' },
  { key: 'monthly', labelKey: 'recurring.presetMonthly' },
  { key: 'custom', labelKey: 'recurring.presetCustom' },
]

const weekdayOptions: Array<{ bit: number, labelKey: string }> = [
  { bit: 1, labelKey: 'recurring.weekdayMon' },
  { bit: 2, labelKey: 'recurring.weekdayTue' },
  { bit: 4, labelKey: 'recurring.weekdayWed' },
  { bit: 8, labelKey: 'recurring.weekdayThu' },
  { bit: 16, labelKey: 'recurring.weekdayFri' },
  { bit: 32, labelKey: 'recurring.weekdaySat' },
  { bit: 64, labelKey: 'recurring.weekdaySun' },
]

interface SelectOption {
  value: number
  label: string
}

// Custom mode only offers the two "every N" modes.
const modeOptions = computed<SelectOption[]>(() => [
  { value: RECURRENCE_MODE.EVERY_N_WEEKS, label: t('recurring.modeEveryNWeeks') },
  { value: RECURRENCE_MODE.EVERY_N_MONTHS, label: t('recurring.presetEveryNMonths') },
])

// Which preset chip is active — 'custom' when the spec doesn't match a preset.
const derivePreset = (s: RecurrenceSpecModel): PresetKey => {
  const mode = Number(s.recurrenceMode)
  if (mode === RECURRENCE_MODE.WEEKLY) return 'weekly'
  if (mode === RECURRENCE_MODE.EVERY_N_WEEKS && Number(s.interval) === 2) return 'everyTwoWeeks'
  if (mode === RECURRENCE_MODE.MONTHLY) return 'monthly'
  return 'custom'
}

const selectedPreset = ref<PresetKey>(derivePreset(spec.value))

// Sticky custom mode: once the chip is 'custom' (whether the user picked it or
// it was derived from a loaded spec), spec changes (including values that
// happen to match a preset) must not snap the chip back to a preset.
// Syncing from the spec resumes when a preset chip is clicked again.
const userChoseCustom = ref(derivePreset(spec.value) === 'custom')

// Keep the chip in sync when the spec changes (async form load) — but not
// while the chip is in custom mode.
watch(() => [spec.value.recurrenceMode, spec.value.interval] as const, () => {
  const derived = derivePreset(spec.value)
  if (derived === 'custom') {
    userChoseCustom.value = true
    selectedPreset.value = 'custom'
  }
  else if (!userChoseCustom.value) {
    selectedPreset.value = derived
  }
})

const update = (patch: Partial<RecurrenceSpecModel>): void => {
  spec.value = { ...spec.value, ...patch }
}

const applyPreset = (preset: PresetKey): void => {
  selectedPreset.value = preset
  userChoseCustom.value = preset === 'custom'

  if (preset === 'custom') {
    // Entering custom mode needs a valid "every N" mode for the select.
    // Default to the closest one based on the current mode.
    const mode = Number(spec.value.recurrenceMode)
    if (mode === RECURRENCE_MODE.EVERY_N_WEEKS || mode === RECURRENCE_MODE.EVERY_N_MONTHS) return
    if (mode === RECURRENCE_MODE.MONTHLY) {
      update({ recurrenceMode: RECURRENCE_MODE.EVERY_N_MONTHS, interval: spec.value.interval ?? 1 })
    }
    else {
      update({ recurrenceMode: RECURRENCE_MODE.EVERY_N_WEEKS, interval: spec.value.interval ?? 2 })
    }
    return
  }

  if (preset === 'weekly') {
    update({
      recurrenceMode: RECURRENCE_MODE.WEEKLY,
      weekdays: (Number(spec.value.weekdays) || 0) || 1,
      dayOfMonth: null,
      interval: null,
    })
    return
  }

  if (preset === 'everyTwoWeeks') {
    update({
      recurrenceMode: RECURRENCE_MODE.EVERY_N_WEEKS,
      weekdays: 0,
      dayOfMonth: null,
      interval: 2,
    })
    return
  }

  // Monthly: derive the day-of-month from the anchor date (forced, not only when null)
  const anchorDay = spec.value.anchorDate ? Number(spec.value.anchorDate.slice(8, 10)) : NaN
  update({
    recurrenceMode: RECURRENCE_MODE.MONTHLY,
    weekdays: 0,
    dayOfMonth: Number.isFinite(anchorDay) ? anchorDay : (spec.value.dayOfMonth ?? 1),
    interval: null,
  })
}

const customMode = computed({
  get: () => Number(spec.value.recurrenceMode),
  set: (mode: number | string) => {
    update({ recurrenceMode: Number(mode) })
  },
})

// Progressive disclosure: per-mode controls. 'custom' exposes the mode select
// plus the controls relevant to the selected mode.
const activeMode = computed(() => Number(spec.value.recurrenceMode))
const showWeekdayPills = computed(() => activeMode.value === RECURRENCE_MODE.WEEKLY)
const showInterval = computed(() =>
  selectedPreset.value === 'custom'
  && (activeMode.value === RECURRENCE_MODE.EVERY_N_WEEKS || activeMode.value === RECURRENCE_MODE.EVERY_N_MONTHS))

// Day-of-month is derived from the anchor date, never entered manually.
const dayOfMonthFromAnchor = (anchorDate: string | null): number | null => {
  const day = anchorDate ? Number(anchorDate.slice(8, 10)) : NaN
  return Number.isFinite(day) ? day : null
}

// Keep spec.dayOfMonth in sync with the anchor date while in MONTHLY mode
// (e.g. when the user changes the start date after picking the monthly preset).
// Note: the watch source returns a fresh array per run, so the callback fires on
// every spec replacement — the `day !== spec.value.dayOfMonth` guard is what
// stops the write → replace → re-fire cycle from recursing forever (which
// aborts the scheduler and drops the pending re-render of the date input).
watch(() => [spec.value.recurrenceMode, spec.value.anchorDate] as const, ([mode, anchorDate]) => {
  if (Number(mode) === RECURRENCE_MODE.MONTHLY && spec.value.dayOfMonth !== null) {
    const day = dayOfMonthFromAnchor(anchorDate)
    if (day !== null && day !== spec.value.dayOfMonth) {
      update({ dayOfMonth: day })
    }
  }
})

const isWeekdaySelected = (bit: number): boolean =>
  ((Number(spec.value.weekdays) || 0) & bit) !== 0

const toggleWeekday = (bit: number): void => {
  update({ weekdays: (Number(spec.value.weekdays) || 0) ^ bit })
}

const toIntOrFallback = (value: number | string | undefined, fallback: number | null, min: number): number | null => {
  const parsed = Math.floor(Number(value))
  if (!Number.isFinite(parsed) || parsed < min) return fallback
  return parsed
}

// --- Debounced occurrence preview -------------------------------------------

const preview = ref<RecurrencePreviewResponse | null>(null)
const previewError = ref(false)

// A preview request is only built once the spec fields are complete enough
// for the backend to evaluate (mirrors RecurrenceEvaluator.Validate).
const previewRequest = computed<RecurrencePreviewRequest | null>(() => {
  const mode = activeMode.value
  const weekdays = Number(spec.value.weekdays) || 0
  if (!spec.value.anchorDate) return null
  if (mode === RECURRENCE_MODE.WEEKLY && weekdays === 0) return null
  if (mode === RECURRENCE_MODE.MONTHLY && spec.value.dayOfMonth == null) return null
  if ((mode === RECURRENCE_MODE.EVERY_N_WEEKS || mode === RECURRENCE_MODE.EVERY_N_MONTHS)
    && (spec.value.interval == null || Number(spec.value.interval) < 1)) return null

  return {
    recurrenceMode: mode,
    weekdays,
    dayOfMonth: mode === RECURRENCE_MODE.MONTHLY ? spec.value.dayOfMonth : null,
    interval: (mode === RECURRENCE_MODE.EVERY_N_WEEKS || mode === RECURRENCE_MODE.EVERY_N_MONTHS)
      ? spec.value.interval
      : null,
    anchorDate: spec.value.anchorDate,
    endDate: spec.value.endDate || null,
  }
})

const runPreview = async (request: RecurrencePreviewRequest): Promise<void> => {
  previewError.value = false
  try {
    preview.value = await previewOccurrences(request) ?? null
  }
  catch {
    // Error shown via toast; surface an inline hint too
    previewError.value = true
  }
}

const debouncedPreview = useDebounceFn((request: RecurrencePreviewRequest) => runPreview(request), 400)

watch(previewRequest, (request) => {
  if (request) {
    void debouncedPreview(request)
  }
  else {
    preview.value = null
    previewError.value = false
  }
}, { immediate: true })
</script>
