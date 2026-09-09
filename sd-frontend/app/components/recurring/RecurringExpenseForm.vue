<template>
  <div class="flex flex-col items-center justify-center py-6 sm:py-8">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <UiCardHeader :title="title" />
      </template>
      <UForm
        :state="model"
        :validate="validate"
        class="space-y-4"
        @submit="onSubmit"
      >
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <UFormField
            :label="$t('expenses.expenseTitle')"
            name="title"
            required
          >
            <UInput
              v-model="model.title"
              :placeholder="$t('expenses.enterTitle')"
              size="lg"
              class="w-full"
              maxlength="255"
            />
          </UFormField>
          <UFormField
            :label="$t('expenses.amount')"
            name="amount"
            required
          >
            <UInput
              :model-value="displayValue('amount', model.amount)"
              type="text"
              inputmode="decimal"
              :placeholder="$t('expenses.enterAmount')"
              size="lg"
              class="w-full"
              @focus="onAmountFocus('amount', model.amount)"
              @update:model-value="v => onAmountInput('amount', v)"
              @blur="onAmountBlur"
            />
          </UFormField>
          <UFormField
            class="sm:col-span-2"
            :label="$t('expenses.description')"
            name="description"
          >
            <UTextarea
              v-model="model.description"
              :placeholder="$t('expenses.enterDescription')"
              size="lg"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('expenses.whoPaid')"
            name="paidByUserId"
            required
          >
            <USelect
              v-model="model.paidByUserId"
              :items="payerOptions"
              :placeholder="$t('expenses.selectWhoPaid')"
              size="lg"
              :loading="isLoadingMembers"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('expenses.category')"
            name="categoryId"
            required
          >
            <USelect
              v-model="model.categoryId"
              :items="categoryOptions"
              :placeholder="$t('expenses.selectCategory')"
              size="lg"
              :loading="isLoadingCategories"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('expenses.paymentMethod')"
            name="paymentModeId"
            required
          >
            <USelect
              v-model="model.paymentModeId"
              :items="paymentModeOptions"
              :placeholder="$t('expenses.selectPaymentMethod')"
              size="lg"
              :loading="isLoadingPaymentModes"
              class="w-full"
            />
          </UFormField>
        </div>

        <!-- Alias setup not finalized notice -->
        <UCard
          v-if="isAliasMode && !aliasSetupFinalized"
          variant="soft"
          color="warning"
          :ui="{ body: 'p-3' }"
        >
          <div class="flex items-start gap-3">
            <UIcon
              name="i-lucide-alert-triangle"
              class="size-5 text-warning shrink-0 mt-0.5"
            />
            <div>
              <p class="text-sm font-semibold text-highlighted">
                {{ $t('groups.aliasSetupNotFinalized') }}
              </p>
              <UButton
                :to="`/groups/${groupId}/members`"
                variant="link"
                color="warning"
                size="xs"
                class="p-0 h-auto mt-1"
              >
                {{ $t('groups.finalizeAliases') }}
              </UButton>
            </div>
          </div>
        </UCard>

        <!-- Split Section -->
        <div class="space-y-2">
          <div class="flex items-center justify-between mb-3">
            <p class="text-sm font-medium text-muted">
              {{ isAliasMode ? $t('expenses.splitBetweenAliases') : $t('expenses.splitBetween') }}
            </p>
            <UButton
              :label="$t('expenses.splitEqually')"
              icon="i-lucide-equal"
              variant="ghost"
              color="primary"
              size="sm"
              :disabled="includedSplits.length < 2 || amountMillis === 0"
              @click="splitEqually"
            />
          </div>
          <div class="space-y-0">
            <template v-if="isAliasMode">
              <div
                v-for="alias in aliases"
                :key="alias.id"
                class="grid grid-cols-[1fr_auto] items-center gap-3 py-2 border-b border-[var(--sd-surface-border)] last:border-0"
              >
                <button
                  type="button"
                  class="flex items-center gap-2 min-w-0 text-left"
                  @click="handleAliasSplitToggle(alias.id, !splitByAlias(alias.id).included)"
                >
                  <UAvatar
                    icon="i-lucide-users"
                    size="sm"
                    :class="splitByAlias(alias.id).included ? 'ring-2 ring-primary bg-primary/10 text-primary' : 'bg-muted/10 text-muted opacity-60'"
                    :alt="alias.name"
                  />
                  <span
                    class="text-sm truncate"
                    :class="splitByAlias(alias.id).included ? 'text-highlighted' : 'text-muted'"
                  >
                    {{ alias.name }}
                  </span>
                </button>
                <UInput
                  :model-value="displayValue('alias-' + alias.id, splitByAlias(alias.id).splitAmount)"
                  type="text"
                  inputmode="decimal"
                  size="sm"
                  class="w-24 text-right sd-tabular"
                  :disabled="!splitByAlias(alias.id).included"
                  @focus="onAmountFocus('alias-' + alias.id, splitByAlias(alias.id).splitAmount)"
                  @update:model-value="v => onSplitAmountInput('alias-' + alias.id, v, splitByAlias(alias.id), () => trackAlias(alias.id))"
                  @blur="onAmountBlur"
                />
              </div>
            </template>
            <template v-else>
              <div
                v-for="member in groupMembers"
                :key="member.userId"
                class="grid grid-cols-[1fr_auto] items-center gap-3 py-2 border-b border-[var(--sd-surface-border)] last:border-0"
              >
                <button
                  type="button"
                  class="flex items-center gap-2 min-w-0 text-left"
                  @click="handleSplitToggle(member.userId, !splitByUser(member.userId).included)"
                >
                  <UserAvatar
                    :user="member.user as UserBasicInfo"
                    size="sm"
                    :class="splitByUser(member.userId).included ? 'ring-2 ring-primary bg-primary/10 text-primary' : 'bg-muted/10 text-muted opacity-60'"
                  />
                  <span
                    class="text-sm truncate"
                    :class="splitByUser(member.userId).included ? 'text-highlighted' : 'text-muted'"
                  >
                    {{ member.user.firstName }} {{ member.user.lastName }}
                  </span>
                </button>
                <UInput
                  :model-value="displayValue('split-' + member.userId, splitByUser(member.userId).splitAmount)"
                  type="text"
                  inputmode="decimal"
                  size="sm"
                  class="w-24 text-right sd-tabular"
                  :disabled="!splitByUser(member.userId).included"
                  @focus="onAmountFocus('split-' + member.userId, splitByUser(member.userId).splitAmount)"
                  @update:model-value="v => onSplitAmountInput('split-' + member.userId, v, splitByUser(member.userId), () => trackUser(member.userId))"
                  @blur="onAmountBlur"
                />
              </div>
              <!-- Orphaned splits: saved splits for members who have since left
                   the group. Display-only — never written into model.value.splits. -->
              <div
                v-for="orphan in orphanedSplits"
                :key="'orphan-' + orphan.userId"
                class="grid grid-cols-[1fr_auto] items-center gap-3 py-2 border-b border-[var(--sd-surface-border)] last:border-0 opacity-70"
              >
                <div class="flex items-center gap-2 min-w-0">
                  <UAvatar
                    size="sm"
                    :alt="$t('recurring.unknownMember')"
                    :class="'bg-muted/10 text-muted opacity-60'"
                  />
                  <span class="min-w-0">
                    <span class="text-sm truncate block text-muted">
                      {{ $t('recurring.unknownMember') }}
                    </span>
                    <UBadge
                      variant="soft"
                      color="warning"
                      size="xs"
                      :label="$t('recurring.leftGroup')"
                      class="mt-0.5"
                    />
                  </span>
                </div>
                <UInput
                  :model-value="displayValue('orphan-' + orphan.userId, orphan.splitAmount)"
                  type="text"
                  inputmode="decimal"
                  size="sm"
                  class="w-24 text-right sd-tabular"
                  disabled
                />
              </div>
            </template>
          </div>
          <!-- Totals feedback -->
          <div class="text-xs space-y-1">
            <div
              v-if="model.amount"
              class="flex justify-between items-center"
            >
              <span class="text-toned">{{ $t('expenses.expenseTotal') }}</span>
              <span class="font-medium">{{ formatCurrency(parseFloat(model.amount), { fullPrecision: true }) }}</span>
            </div>
            <div
              v-if="splitTotal > 0"
              class="flex justify-between items-center"
            >
              <span class="text-toned">{{ $t('expenses.splitTotal') }}</span>
              <span class="font-medium">{{ formatCurrency(splitTotal, { fullPrecision: true }) }}</span>
            </div>
            <div
              v-if="model.amount && remainingMillis !== 0"
              class="flex justify-between items-center font-semibold"
              :class="{
                'text-error': remainingMillis < 0,
                'text-warning': remainingMillis > 0,
              }"
            >
              <span>{{ remainingMillis > 0 ? $t('expenses.remaining') : $t('expenses.overBy') }}</span>
              <span>{{ formatCurrency(Math.abs(remainingAmount), { fullPrecision: true }) }}</span>
            </div>
            <div
              v-if="model.amount && remainingMillis === 0"
              class="flex justify-between items-center text-success font-semibold"
            >
              <span>{{ $t('expenses.splitsBalanced') }}</span>
            </div>
            <div
              v-if="orphanedTotalMillis > 0"
              class="text-warning"
            >
              {{ $t('recurring.orphanedSplitsRedistributed', { amount: formatCurrency(fromMillis(orphanedTotalMillis), { fullPrecision: true }) }) }}
            </div>
          </div>
        </div>

        <!-- Recurrence schedule -->
        <USeparator class="my-2" />
        <div class="space-y-2">
          <h3 class="text-sm font-semibold text-highlighted">
            {{ $t('recurring.scheduleSection') }}
          </h3>
          <UFormField
            name="requiresApproval"
            :description="$t('recurring.requiresApprovalDescription')"
          >
            <div class="flex items-center gap-3">
              <USwitch
                v-model="model.requiresApproval"
              />
              <span class="text-sm">{{ $t('recurring.requiresApproval') }}</span>
            </div>
          </UFormField>
          <RecurringRecurrenceBuilder
            v-model="recurrenceSpec"
            :group-id="groupId"
          />
        </div>

        <div class="flex flex-col-reverse sm:flex-row sm:justify-end gap-3 mt-6">
          <UButton
            :label="$t('expenses.cancel')"
            variant="ghost"
            color="neutral"
            class="grow sm:grow-0"
            @click="emit('cancel')"
          />
          <UButton
            type="submit"
            :label="submitLabel"
            :loading="loading"
            :disabled="!canSubmit"
            class="grow sm:grow-0"
          />
        </div>
      </UForm>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import type { GroupMember, UserBasicInfo } from '~/types/domain'
import { splitMillis, rescaleMillis, fromMillis, toMillis, formatCurrency } from '~/utils/currency'

const props = withDefaults(defineProps<{
  title: string
  submitLabel: string
  groupId: string
  loading?: boolean
  // Splits saved on the template whose member has since left the group.
  // Display-only: rendered as disabled orphan rows, never written into the
  // form model (the backend rejects splits for non-members).
  orphanedSplits?: RecurringFormSplit[]
}>(), {
  orphanedSplits: () => [],
})

export interface RecurringFormSplit {
  userId: string
  included: boolean
  splitAmount: number | null
}

export interface RecurringFormAliasSplit {
  aliasId: string
  included: boolean
  splitAmount: number | null
}

export interface RecurringFormModel {
  templateId?: string
  title: string
  description: string
  amount: string
  paidByUserId: string
  categoryId?: number
  paymentModeId?: number
  requiresApproval: boolean
  splits: RecurringFormSplit[]
  aliasSplits: RecurringFormAliasSplit[]
  recurrence: RecurrenceSpec
}

export interface RecurrenceSpec {
  recurrenceMode: number
  weekdays: number
  dayOfMonth: number | null
  interval: number | null
  anchorDate: string | null
  endDate: string | null
}

export interface RecurringFormPayload {
  expense: {
    title: string
    description: string | null
    amount: number
    categoryId?: number
    paymentModeId?: number
    paidByUserId: string
    requiresApproval: boolean
  }
  recurrence: RecurrenceSpec
  splits?: { userId: string, splitAmount: number }[]
  aliasSplits?: { aliasId: string, splitAmount: number }[]
}

const model = defineModel<RecurringFormModel>({ required: true })

const emit = defineEmits<{
  submit: [payload: RecurringFormPayload]
  cancel: []
}>()

const { t } = useI18n()
const { user } = useAuth()
const { fetchGroup, fetchGroupMembers, currentGroup } = useGroups()
const { aliases, fetchAliases } = useAliases()
const { categories, isLoading: isLoadingCategories } = useCategories()
const { paymentModes, isLoading: isLoadingPaymentModes } = usePaymentModes()

const isLoadingMembers = ref(false)
const groupMembers = ref<GroupMember[]>([])

const group = computed(() =>
  currentGroup.value?.id === props.groupId ? currentGroup.value : null)

const isAliasMode = computed(() => !!group.value?.useAliases)
const aliasSetupFinalized = computed(() => !!group.value?.aliasSetupFinalized)
const canCreateTemplate = computed(() => !isAliasMode.value || aliasSetupFinalized.value)

const recurrenceSpec = computed({
  get: () => model.value.recurrence,
  set: (value: RecurrenceSpec) => {
    model.value.recurrence = value
  },
})

// --- Amount input handling (same conventions as ExpensesExpenseForm) ---------

const parseAmount = (raw: string | null | undefined): number | null => {
  if (raw === '' || raw === null || raw === undefined) return null

  let normalized = String(raw).replace(/,/g, '.')
  normalized = normalized.replace(/[^\d.]/g, '')

  const firstDot = normalized.indexOf('.')
  if (firstDot !== -1) {
    const before = normalized.slice(0, firstDot + 1)
    const after = normalized.slice(firstDot + 1).replace(/\./g, '')
    normalized = before + after
  }

  if (normalized.endsWith('.')) normalized = normalized.slice(0, -1)

  const n = Number(normalized)
  if (!Number.isFinite(n) || normalized === '') return null
  return n
}

const handleAmountInput = (raw: string, field: string, target: RecurringFormSplit | RecurringFormAliasSplit | null, onChange?: () => void): void => {
  const parsed = parseAmount(raw)

  if (target) {
    ;(target as unknown as Record<string, unknown>)[field] = parsed ?? null
  }
  else {
    ;(model.value as unknown as Record<string, unknown>)[field] = parsed ?? null
    updateSplits()
  }

  onChange?.()
}

// Display formatting is applied on blur, not on every keystroke, so the
// formatted value doesn't fight typing while the field is focused.
const editingField = ref<string | null>(null)
const editingValue = ref<string>('')

const displayValue = (id: string, numericVal: string | number | null | undefined): string =>
  editingField.value === id
    ? editingValue.value
    : (numericVal == null ? '' : formatAmount(numericVal))

const onAmountFocus = (id: string, numericVal: string | number | null | undefined): void => {
  editingField.value = id
  editingValue.value = numericVal == null ? '' : String(numericVal)
}

const onAmountInput = (field: string, raw: string): void => {
  editingValue.value = raw
  handleAmountInput(raw, field, null)
}

const onSplitAmountInput = (id: string, raw: string, target: RecurringFormSplit | RecurringFormAliasSplit, onChange?: () => void): void => {
  editingValue.value = raw
  handleAmountInput(raw, 'splitAmount', target, onChange)
}

const onAmountBlur = (): void => {
  editingField.value = null
  editingValue.value = ''
}

// --- Split calculations ------------------------------------------------------

const amountMillis = computed(() => toMillis(model.value.amount ?? ''))

const activeSplits = computed(() =>
  isAliasMode.value ? model.value.aliasSplits : model.value.splits)

const includedSplits = computed(() => activeSplits.value.filter(s => s.included))

const splitTotalMillis = computed(() =>
  activeSplits.value
    .filter(s => s.included)
    .reduce((total, s) => total + toMillis(s.splitAmount ?? 0), 0))

const remainingMillis = computed(() => amountMillis.value - splitTotalMillis.value)

const splitTotal = computed(() => fromMillis(splitTotalMillis.value))
const remainingAmount = computed(() => fromMillis(remainingMillis.value))

// Sum of amounts saved for members who have since left the group — shown once
// in the totals block as the amount that was redistributed to remaining members.
const orphanedTotalMillis = computed(() =>
  props.orphanedSplits.reduce((total, s) => total + toMillis(s.splitAmount ?? 0), 0))

// Returns a LIVE reference into model.value.splits, creating the entry if
// missing so v-model mutations in the template are never silently lost.
const splitByUser = (userId: string): RecurringFormSplit => {
  let s = model.value.splits.find(x => x.userId === userId)
  if (!s) {
    s = { userId, included: false, splitAmount: 0 }
    model.value.splits.push(s)
  }
  return s
}

const splitByAlias = (aliasId: string): RecurringFormAliasSplit => {
  let s = model.value.aliasSplits.find(x => x.aliasId === aliasId)
  if (!s) {
    s = { aliasId, included: false, splitAmount: 0 }
    model.value.aliasSplits.push(s)
  }
  return s
}

const assignShares = (splits: (RecurringFormSplit | RecurringFormAliasSplit)[], sharesMillis: number[]): void => {
  splits.forEach((s, i) => {
    s.splitAmount = fromMillis(sharesMillis[i] ?? 0)
  })
}

// Rescale to target, but re-equalize if the current values were a fair-remainder
// equal split (differ by ≤1 millicent) — otherwise proportional scaling would
// turn [23.334, 23.333] into [35.001, 34.999] instead of [35, 35].
const redistributeMillis = (currentMillis: number[], targetMillis: number): number[] => {
  if (currentMillis.length === 0) return []
  const spread = Math.max(...currentMillis) - Math.min(...currentMillis)
  return spread <= 1
    ? splitMillis(targetMillis, currentMillis.length)
    : rescaleMillis(currentMillis, targetMillis)
}

const lastModifiedEntityId = ref<string | null>(null)

const trackUser = (userId: string): void => {
  lastModifiedEntityId.value = userId
}

const trackAlias = (aliasId: string): void => {
  lastModifiedEntityId.value = aliasId
}

// Toggle-off: rescale remaining included to keep the total (preserves ratios).
// Toggle-on: give the new participant an equal share, rescale others to fit.
const handleSplitToggle = (userId: string, included: boolean): void => {
  const split = splitByUser(userId)
  split.included = included

  if (!model.value.amount) return
  const includedList = model.value.splits.filter(s => s.included)
  if (includedList.length === 0) return

  if (included) {
    const perPerson = Math.floor(amountMillis.value / includedList.length)
    split.splitAmount = fromMillis(perPerson)
    const others = includedList.filter(s => s.userId !== userId)
    if (others.length > 0) {
      const otherCurrent = others.map(s => toMillis(s.splitAmount ?? 0))
      const rescaled = redistributeMillis(otherCurrent, amountMillis.value - perPerson)
      assignShares(others, rescaled)
    }
  }
  else {
    const currentMillis = includedList.map(s => toMillis(s.splitAmount ?? 0))
    const rescaled = redistributeMillis(currentMillis, amountMillis.value)
    assignShares(includedList, rescaled)
  }
}

const handleAliasSplitToggle = (aliasId: string, included: boolean): void => {
  const split = splitByAlias(aliasId)
  split.included = included

  if (!model.value.amount) return
  const includedList = model.value.aliasSplits.filter(s => s.included)
  if (includedList.length === 0) return

  if (included) {
    const perAlias = Math.floor(amountMillis.value / includedList.length)
    split.splitAmount = fromMillis(perAlias)
    const others = includedList.filter(s => s.aliasId !== aliasId)
    if (others.length > 0) {
      const otherCurrent = others.map(s => toMillis(s.splitAmount ?? 0))
      const rescaled = redistributeMillis(otherCurrent, amountMillis.value - perAlias)
      assignShares(others, rescaled)
    }
  }
  else {
    const currentMillis = includedList.map(s => toMillis(s.splitAmount ?? 0))
    const rescaled = redistributeMillis(currentMillis, amountMillis.value)
    assignShares(includedList, rescaled)
  }
}

const splitEqually = (): void => {
  const included = activeSplits.value.filter(s => s.included)
  if (included.length === 0 || amountMillis.value === 0) return
  const shares = splitMillis(amountMillis.value, included.length)
  assignShares(included, shares)
}

// --- Group data --------------------------------------------------------------

const loadGroupData = async (): Promise<void> => {
  if (!props.groupId) return

  isLoadingMembers.value = true
  try {
    await fetchGroup(props.groupId)
    const members = await fetchGroupMembers(props.groupId) || []
    groupMembers.value = members

    if (isAliasMode.value) {
      await fetchAliases(props.groupId)
    }

    if (!model.value.paidByUserId) {
      const currentUserMember = members.find(m => m.userId === user.value?.id)
      if (currentUserMember) {
        model.value.paidByUserId = user.value!.id
      }
      else if (members.length > 0) {
        model.value.paidByUserId = members[0]!.userId
      }
    }
  }
  catch (error: unknown) {
    console.error('Failed to load group data:', error)
  }
  finally {
    isLoadingMembers.value = false
  }
}

onMounted(async () => {
  await loadGroupData()
})

// Re-sync splits with the current member/alias list — preserves existing
// splits, drops ones for removed entities, adds zero-entries for newcomers.
watch([groupMembers, aliases], (): void => {
  if (isAliasMode.value) {
    if (aliases.value.length > 0) updateSplits()
    return
  }
  if (groupMembers.value.length > 0) updateSplits()
}, { immediate: true })

const updateSplits = (): void => {
  if (isAliasMode.value) {
    updateAliasSplits()
    return
  }
  updateUserSplits()
}

const updateUserSplits = (): void => {
  const members = groupMembers.value

  const byId = new Map(model.value.splits.map(s => [s.userId, s]))
  model.value.splits = members.map((m) => {
    const existing = byId.get(m.userId)
    if (existing) return existing
    // New entries start included with an equal share when the amount is known
    const included = amountMillis.value > 0
    const share = included ? Math.floor(amountMillis.value / members.length) : 0
    return { userId: m.userId, included, splitAmount: fromMillis(share) }
  })

  rescaleIncluded()
}

const updateAliasSplits = (): void => {
  const aliasList = aliases.value

  const byId = new Map(model.value.aliasSplits.map(s => [s.aliasId, s]))
  model.value.aliasSplits = aliasList.map((a) => {
    const existing = byId.get(a.id)
    if (existing) return existing
    const included = amountMillis.value > 0
    const share = included ? Math.floor(amountMillis.value / aliasList.length) : 0
    return { aliasId: a.id, included, splitAmount: fromMillis(share) }
  })

  rescaleIncluded()
}

// After re-sync, included splits should sum exactly to the amount.
const rescaleIncluded = (): void => {
  const included = activeSplits.value.filter(s => s.included)
  if (amountMillis.value === 0 || included.length === 0) return
  // Respect a manual edit the user just made
  const lastModified = included.find(s =>
    (isAliasMode.value
      ? (s as RecurringFormAliasSplit).aliasId
      : (s as RecurringFormSplit).userId) === lastModifiedEntityId.value)
  if (lastModified) return

  const currentTotalMillis = included.reduce((total, s) => total + toMillis(s.splitAmount ?? 0), 0)
  if (currentTotalMillis === 0) {
    assignShares(included, splitMillis(amountMillis.value, included.length))
    return
  }
  if (currentTotalMillis !== amountMillis.value) {
    const rescaled = redistributeMillis(
      included.map(s => toMillis(s.splitAmount ?? 0)),
      amountMillis.value,
    )
    assignShares(included, rescaled)
  }
}

// --- Select options ----------------------------------------------------------

interface SelectOption {
  value: string | number
  label: string
}

// Payer is always a group member (user) — in alias-mode groups the backend
// resolves the payer's alias server-side, so `paidByUserId` is always a user guid.
const payerOptions = computed<SelectOption[]>(() => {
  return groupMembers.value.map(member => ({
    value: member.userId,
    label: `${member.user.firstName} ${member.user.lastName}`,
  }))
})

const categoryOptions = computed<SelectOption[]>(() => {
  return categories.value.map(category => ({
    value: Number(category.id),
    label: category.name,
  }))
})

const paymentModeOptions = computed<SelectOption[]>(() => {
  return paymentModes.value.map(mode => ({
    value: Number(mode.id),
    label: mode.name,
  }))
})

// --- Validation --------------------------------------------------------------

interface ValidationError {
  name: string
  message: string
}

const validate = (): ValidationError[] => {
  const errors: ValidationError[] = []
  if (!model.value.title?.trim()) {
    errors.push({ name: 'title', message: t('expenses.titleRequired') })
  }
  if (!model.value.amount) {
    errors.push({ name: 'amount', message: t('expenses.amountRequired') })
  }
  if (!model.value.paidByUserId) {
    errors.push({ name: 'paidByUserId', message: t('expenses.paidByRequired') })
  }
  if (!model.value.categoryId) {
    errors.push({ name: 'categoryId', message: t('expenses.categoryRequired') })
  }
  if (!model.value.paymentModeId) {
    errors.push({ name: 'paymentModeId', message: t('expenses.paymentModeRequired') })
  }

  const splitEntityLabel = isAliasMode.value ? t('expenses.alias') : t('expenses.person')

  if (includedSplits.value.length === 0) {
    errors.push({ name: 'splits', message: t('expenses.atLeastOneSplit', { entity: splitEntityLabel }) })
  }
  else if (model.value.amount && remainingMillis.value !== 0) {
    errors.push({
      name: 'splits',
      message: t('expenses.splitTotalMustEqual', {
        total: formatCurrency(splitTotal.value, { fullPrecision: true }),
        amount: formatCurrency(parseFloat(model.value.amount), { fullPrecision: true }),
      }),
    })
  }

  if (!model.value.recurrence.anchorDate) {
    errors.push({ name: 'recurrence.anchorDate', message: t('recurring.anchorDateRequired') })
  }

  return errors
}

const canSubmit = computed(() => canCreateTemplate.value)

// --- Submit ------------------------------------------------------------------

const onSubmit = (): void => {
  const payload: RecurringFormPayload = {
    expense: {
      title: model.value.title?.trim() ?? '',
      description: model.value.description || null,
      amount: parseAmount(model.value.amount) ?? 0,
      categoryId: model.value.categoryId || undefined,
      paymentModeId: model.value.paymentModeId || undefined,
      paidByUserId: model.value.paidByUserId,
      requiresApproval: model.value.requiresApproval,
    },
    recurrence: { ...model.value.recurrence },
  }

  if (isAliasMode.value) {
    payload.aliasSplits = model.value.aliasSplits
      .filter(s => s.included)
      .map(s => ({ aliasId: s.aliasId, splitAmount: Number(s.splitAmount) || 0 }))
      .filter(s => s.splitAmount > 0)
  }
  else {
    payload.splits = model.value.splits
      .filter(s => s.included)
      .map(s => ({ userId: s.userId, splitAmount: Number(s.splitAmount) || 0 }))
      .filter(s => s.splitAmount > 0)
  }

  emit('submit', payload)
}
</script>
