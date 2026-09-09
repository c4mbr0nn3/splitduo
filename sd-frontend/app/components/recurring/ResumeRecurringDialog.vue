<template>
  <UModal
    v-model:open="isOpen"
    :dismissible="!isSubmitting"
  >
    <template #header>
      <div class="flex items-start gap-3">
        <UIcon
          name="i-lucide-pause-circle"
          class="size-6 text-warning shrink-0 mt-0.5"
        />
        <div class="flex-1 min-w-0">
          <h3 class="text-lg font-semibold">
            {{ $t('recurring.resume.missedTitle', { count: missedCount }, missedCount) }}
          </h3>
          <p class="text-sm text-muted truncate">
            {{ template.title }}
          </p>
        </div>
      </div>
    </template>

    <template #body>
      <div class="space-y-3">
        <p class="text-sm text-toned">
          {{ $t('recurring.resume.missedContent', { count: missedCount }, missedCount) }}
        </p>
        <ul
          v-if="missedDates.length"
          class="text-xs text-muted space-y-1 sd-tabular"
        >
          <li
            v-for="date in missedDates"
            :key="date"
          >
            {{ formatDateString(date) }}
          </li>
        </ul>
      </div>
    </template>

    <template #footer>
      <div class="flex flex-col gap-2 w-full sm:flex-row sm:justify-end">
        <UButton
          color="neutral"
          variant="ghost"
          :disabled="isSubmitting"
          class="w-full sm:w-auto"
          :label="$t('recurring.resume.cancel')"
          @click="close"
        />
        <UButton
          color="neutral"
          variant="outline"
          :disabled="isSubmitting"
          class="w-full sm:w-auto"
          :label="$t('recurring.resume.backfillButton')"
          @click="resume('backfill')"
        />
        <UButton
          :loading="isSubmitting"
          :disabled="isSubmitting"
          class="w-full sm:w-auto"
          :label="$t('recurring.resume.skipButton')"
          @click="resume('skip')"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { DeepReadonly } from 'vue'
import type { RecurringExpenseTemplate, ResumeStrategy } from '~/types/domain'

const props = defineProps<{
  template: DeepReadonly<RecurringExpenseTemplate>
}>()

const emit = defineEmits<{
  resume: [strategy: ResumeStrategy]
  cancel: []
}>()

const isOpen = defineModel<boolean>('open', { default: true })

const isSubmitting = ref(false)

const missedCount = computed(() => Number(props.template.missedCount) || 0)

const missedDates = computed(() => props.template.skippedPeriodDates ?? [])

const resume = (strategy: ResumeStrategy): void => {
  emit('resume', strategy)
}

// Explicit cancel button: closing the modal triggers the watch below,
// which emits cancel and leaves the template paused.
const close = (): void => {
  isOpen.value = false
}

const onCancel = (): void => {
  emit('cancel')
}

// Closing via the overlay/ESC (dismissible) leaves the template paused
watch(isOpen, (open) => {
  if (!open) onCancel()
})
</script>
