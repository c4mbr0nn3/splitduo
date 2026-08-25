<template>
  <div class="flex flex-col items-center justify-center py-6 sm:py-8">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <div class="flex items-center justify-between">
          <UiCardHeader :title="title" />
          <div class="flex items-center gap-1">
            <UButton
              icon="i-lucide-image-plus"
              :label="$t('expenses.attachments.add')"
              size="sm"
              variant="ghost"
              @click="attachmentInput?.click()"
            />
            <UButton
              v-if="receiptImageUrl"
              icon="i-lucide-image"
              :label="$t('expenses.viewReceipt')"
              size="sm"
              variant="ghost"
              @click="isReceiptPreviewOpen = true"
            />
          </div>
        </div>
        <input
          ref="attachmentInput"
          type="file"
          accept=".jpg,.jpeg,.png,.webp,.heic,.heif,.pdf"
          class="hidden"
          @change="onAttachmentSelected"
        >
      </template>
      <UForm
        :state="model"
        :validate="validate"
        class="space-y-4"
        @submit="onSubmit"
      >
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <UFormField
            v-if="!preSelectedGroupId"
            class="sm:col-span-2"
            :label="$t('expenses.group')"
            name="groupId"
            required
          >
            <USelect
              v-model="model.groupId"
              :items="groupOptions"
              :placeholder="$t('expenses.selectGroup')"
              size="lg"
              :loading="isLoadingGroups"
              class="w-full"
            />
          </UFormField>
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
              @update:model-value="v => onAmountInput('amount', v, 'amount')"
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
              :items="memberOptions"
              :placeholder="$t('expenses.selectWhoPaid')"
              size="lg"
              :disabled="!model.groupId"
              :loading="isLoadingMembers"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('expenses.date')"
            name="expenseDate"
            required
          >
            <UiInputDate
              v-model="model.expenseDate"
              size="lg"
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
          class="mb-2"
          :ui="{ body: 'p-3' }"
        >
          <div class="flex items-start gap-3">
            <UIcon
              name="i-lucide-alert-triangle"
              class="size-5 text-warning shrink-0 mt-0.5"
            />
            <div>
              <p class="text-sm font-semibold text-highlighted">
                {{ $t('expenses.aliasSetupNotFinalized') }}
              </p>
              <UButton
                :to="`/groups/${effectiveGroupId}/aliases`"
                variant="link"
                color="warning"
                size="xs"
                class="p-0 h-auto mt-1"
              >
                {{ $t('expenses.finalizeAliases') }}
              </UButton>
            </div>
          </div>
        </UCard>

        <!-- Split Section -->
        <div class="space-y-2">
          <URadioGroup
            :model-value="splitMode"
            :items="modeOptions"
            variant="list"
            orientation="horizontal"
            size="md"
            class="w-full mb-3"
            :aria-label="$t('expenses.splitMode')"
            @update:model-value="(v) => onModeChange(v as SplitMode)"
          />
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
                class="grid grid-cols-[1fr_auto_auto] items-center gap-3 py-2 border-b border-[var(--sd-surface-border)] last:border-0"
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
                  <span class="min-w-0">
                    <span
                      class="text-sm truncate block"
                      :class="splitByAlias(alias.id).included ? 'text-highlighted' : 'text-muted'"
                    >
                      {{ alias.name }}
                    </span>
                    <UBadge
                      v-if="alias.isSingleton"
                      variant="soft"
                      color="secondary"
                      :label="$t('members.singleton')"
                      size="xs"
                      class="mt-0.5"
                    />
                  </span>
                </button>
                <!-- Percentage mode -->
                <template v-if="splitMode === 'percentage'">
                  <span class="text-xs text-muted min-w-[3rem] text-right sd-tabular">
                    {{ formatCurrency(percentageAmountFor(alias.id), { fullPrecision: true }) }}
                  </span>
                  <UInput
                    :model-value="displayValue('pct-' + alias.id, splitByAlias(alias.id).splitPercentage)"
                    type="text"
                    inputmode="decimal"
                    size="sm"
                    class="w-20 text-right sd-tabular"
                    placeholder="%"
                    :disabled="!splitByAlias(alias.id).included || amountMillis === 0 || isSingleIncluded"
                    :aria-label="`${alias.name} ${$t('expenses.splitByPercentage')}`"
                    @focus="onAmountFocus('pct-' + alias.id, splitByAlias(alias.id).splitPercentage)"
                    @update:model-value="v => onPercentageInput(splitByAlias(alias.id), v)"
                    @blur="onPercentageBlur(splitByAlias(alias.id))"
                  />
                  <span
                    v-if="splitByAlias(alias.id).included && (splitByAlias(alias.id).splitPercentage ?? 0) > 0 && percentageAmountFor(alias.id) === 0"
                    class="text-xs text-warning"
                  >
                    {{ $t('expenses.shareTooSmall') }}
                  </span>
                </template>
                <!-- Amounts mode -->
                <template v-else>
                  <span class="text-xs text-muted min-w-[2.5rem] text-right">
                    {{ model.amount && (splitByAlias(alias.id).splitAmount ?? 0) > 0 ? `${getAliasSplitPercentage(alias.id)}%` : '' }}
                  </span>
                  <UInput
                    :model-value="displayValue('alias-' + alias.id, splitByAlias(alias.id).splitAmount)"
                    type="text"
                    inputmode="decimal"
                    size="sm"
                    class="w-24 text-right sd-tabular"
                    :disabled="!splitByAlias(alias.id).included"
                    @focus="onAmountFocus('alias-' + alias.id, splitByAlias(alias.id).splitAmount)"
                    @update:model-value="v => onAmountInput('alias-' + alias.id, v, 'splitAmount', splitByAlias(alias.id), () => trackAlias(alias.id))"
                    @blur="onAmountBlur"
                  />
                </template>
              </div>
            </template>
            <template v-else>
              <div
                v-for="member in groupMembers"
                :key="member.userId"
                class="grid grid-cols-[1fr_auto_auto] items-center gap-3 py-2 border-b border-[var(--sd-surface-border)] last:border-0"
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
                <!-- Percentage mode -->
                <template v-if="splitMode === 'percentage'">
                  <span class="text-xs text-muted min-w-[3rem] text-right sd-tabular">
                    {{ formatCurrency(percentageAmountFor(member.userId), { fullPrecision: true }) }}
                  </span>
                  <UInput
                    :model-value="displayValue('pct-' + member.userId, splitByUser(member.userId).splitPercentage)"
                    type="text"
                    inputmode="decimal"
                    size="sm"
                    class="w-20 text-right sd-tabular"
                    placeholder="%"
                    :disabled="!splitByUser(member.userId).included || amountMillis === 0 || isSingleIncluded"
                    :aria-label="`${member.user.firstName} ${member.user.lastName} ${$t('expenses.splitByPercentage')}`"
                    @focus="onAmountFocus('pct-' + member.userId, splitByUser(member.userId).splitPercentage)"
                    @update:model-value="v => onPercentageInput(splitByUser(member.userId), v)"
                    @blur="onPercentageBlur(splitByUser(member.userId))"
                  />
                  <span
                    v-if="splitByUser(member.userId).included && (splitByUser(member.userId).splitPercentage ?? 0) > 0 && percentageAmountFor(member.userId) === 0"
                    class="text-xs text-warning"
                  >
                    {{ $t('expenses.shareTooSmall') }}
                  </span>
                </template>
                <!-- Amounts mode -->
                <template v-else>
                  <span class="text-xs text-muted min-w-[2.5rem] text-right">
                    {{ model.amount && (splitByUser(member.userId).splitAmount ?? 0) > 0 ? `${getSplitPercentage(member.userId)}%` : '' }}
                  </span>
                  <UInput
                    :model-value="displayValue('split-' + member.userId, splitByUser(member.userId).splitAmount)"
                    type="text"
                    inputmode="decimal"
                    size="sm"
                    class="w-24 text-right sd-tabular"
                    :disabled="!splitByUser(member.userId).included"
                    @focus="onAmountFocus('split-' + member.userId, splitByUser(member.userId).splitAmount)"
                    @update:model-value="v => onAmountInput('split-' + member.userId, v, 'splitAmount', splitByUser(member.userId), () => trackUser(member.userId))"
                    @blur="onAmountBlur"
                  />
                </template>
              </div>
            </template>
          </div>
          <!-- Allocation feedback (percentage mode only) -->
          <div
            v-if="splitMode === 'percentage' && amountMillis > 0"
            class="space-y-2 pt-2"
          >
            <div class="flex items-center justify-between">
              <span class="text-xs text-muted">{{ $t('expenses.splitAllocation') }}</span>
              <UBadge
                :color="badgeColor"
                variant="soft"
                size="xs"
                :label="badgeLabel"
              />
            </div>
            <UProgress
              :model-value="percentageSum"
              :max="100"
              size="sm"
              :color="progressColor"
            />
            <p
              class="text-xs text-muted sr-only"
              aria-live="polite"
            >
              {{ badgeLabel }}
            </p>
          </div>
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
          </div>
        </div>

        <div class="flex flex-col-reverse sm:flex-row sm:justify-end gap-3 mt-6">
          <UButton
            :label="$t('expenses.cancel')"
            variant="ghost"
            color="neutral"
            class="grow sm:grow-0"
            @click="goBack"
          />
          <UFieldGroup size="lg">
            <UTooltip
              :text="splitMode === 'percentage' && !isPercentageBalanced ? $t('expenses.saveDisabledTooltip') : undefined"
              :disabled="splitMode !== 'percentage' || isPercentageBalanced"
            >
              <UButton
                type="submit"
                :label="submitLabel"
                :loading="loading"
                :disabled="!canSubmit"
                class="grow sm:grow-0"
              />
            </UTooltip>
            <UDropdownMenu
              v-if="showAddMore"
              :items="addMoreMenuItems"
            >
              <UButton
                type="button"
                variant="subtle"
                icon="i-lucide-chevron-down"
                :loading="loading"
                :disabled="!canSubmit"
              />
            </UDropdownMenu>
          </UFieldGroup>
        </div>

        <!-- Selected receipts -->
        <div
          v-if="selectedFiles.length > 0"
          class="space-y-2 pt-2"
        >
          <p class="text-sm font-medium text-muted">
            {{ $t('expenses.attachments.selectedFiles', { count: selectedFiles.length }) }}
          </p>
          <ul class="space-y-2">
            <li
              v-for="(file, index) in selectedFiles"
              :key="file.name + index"
              class="flex items-center justify-between gap-3 rounded-md bg-muted/30 px-3 py-2"
            >
              <div class="flex items-center gap-2 min-w-0">
                <UIcon
                  name="i-lucide-file-image"
                  class="size-4 text-muted shrink-0"
                />
                <span class="text-sm truncate">{{ file.name }}</span>
              </div>
              <UButton
                icon="i-lucide-x"
                variant="ghost"
                color="neutral"
                size="xs"
                square
                :aria-label="$t('expenses.attachments.removeFile')"
                @click="removeSelectedFile(index)"
              />
            </li>
          </ul>
        </div>
      </UForm>
    </UCard>
    <UiReceiptPreviewModal
      v-model="isReceiptPreviewOpen"
      :image-url="receiptImageUrl"
    />
  </div>
</template>

<script setup lang="ts">
import type { GroupMember, CreateExpenseSplit, CreateExpenseAliasSplit, SplitMode, UserBasicInfo } from '~/types/domain'

const { t } = useI18n()

const props = defineProps<{
  title: string
  submitLabel: string
  loading?: boolean
  preSelectedGroupId?: string | null
  showAddMore?: boolean
  expenseId?: string | null
}>()

interface ExpenseFormSplit {
  userId: string
  included: boolean
  splitAmount: number | null
  splitPercentage?: number | null
}

interface ExpenseFormAliasSplit {
  aliasId: string
  included: boolean
  splitAmount: number | null
  splitPercentage?: number | null
}

interface ExpenseFormModel {
  expenseId?: string
  groupId?: string
  title?: string
  description?: string
  amount?: string
  paidByUserId?: string
  expenseDate?: string
  categoryId?: number
  paymentModeId?: number
  splits?: ExpenseFormSplit[]
  aliasSplits?: ExpenseFormAliasSplit[]
  splitMode?: SplitMode
}

const model = defineModel<ExpenseFormModel>({ default: () => ({}) })

const emit = defineEmits<{
  submit: [payload: CreateExpensePayload]
  addMore: [payload: CreateExpensePayload]
  cancel: []
  addAttachment: [file: File]
  removeAttachment: [file: File]
}>()

interface CreateExpensePayload {
  groupId: string
  expenseData: {
    title: string | null
    description: string | null
    amount: number
    paidByUserId: string | null
    expenseDate: string | null
    categoryId: number | undefined
    paymentModeId: number | undefined
    splits?: CreateExpenseSplit[]
    aliasSplits?: CreateExpenseAliasSplit[]
  }
}

const { receiptImageUrl } = useReceiptScan()
const isReceiptPreviewOpen = ref(false)
const isEditMode = computed(() => !!model.value?.expenseId)

// Attachment upload — the form only picks the file and emits it; the parent
// page owns the upload (it has the expenseId in edit mode).
const attachmentInput = ref<HTMLInputElement | null>(null)
const selectedFiles = ref<File[]>([])

const onAttachmentSelected = (event: Event): void => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return

  if (isEditMode.value) {
    emit('addAttachment', file)
    target.value = ''
    return
  }

  selectedFiles.value.push(file)
  emit('addAttachment', file)
  target.value = ''
}

const removeSelectedFile = (index: number): void => {
  const file = selectedFiles.value[index]
  if (!file) return

  selectedFiles.value.splice(index, 1)
  emit('removeAttachment', file)
}

const { user } = useAuth()
const { groups, fetchGroups, fetchGroup, currentGroup, fetchGroupMembers, isLoading: isLoadingGroups } = useGroups()
const { aliases, fetchAliases } = useAliases()
const { categories, isLoading: isLoadingCategories } = useCategories()
const { paymentModes, isLoading: isLoadingPaymentModes } = usePaymentModes()

// Amount input handling --------------------------------------------------------

/**
 * Normalize an amount string:
 * - comma is always treated as the decimal separator, translated to a dot
 * - any thousands separators / non-numeric characters are stripped
 * - only one decimal separator is allowed; a second separator stops parsing
 * - amounts are positive here, so no leading minus support needed
 *
 * The rule for a second separator is "keep the first separator, drop everything
 * after the second one". This means typing "10.5.2" becomes "10.5" rather than
 * jumping to "1052".
 */
const parseAmount = (raw: string | null | undefined): number | null => {
  if (raw === '' || raw === null || raw === undefined) return null

  // First, translate comma to dot so both behave as the same decimal separator.
  let normalized = String(raw).replace(/,/g, '.')

  // Strip every character that isn't a digit or a dot.
  normalized = normalized.replace(/[^\d.]/g, '')

  // Keep only the first dot; drop everything after a second one.
  const firstDot = normalized.indexOf('.')
  if (firstDot !== -1) {
    const before = normalized.slice(0, firstDot + 1)
    const after = normalized.slice(firstDot + 1).replace(/\./g, '')
    normalized = before + after
  }

  // Remove a trailing dot so the model stays numeric while the user is typing.
  if (normalized.endsWith('.')) normalized = normalized.slice(0, -1)

  const n = Number(normalized)
  if (!Number.isFinite(n) || normalized === '') return null
  return n
}

const handleAmountInput = (raw: string, field: string, target: ExpenseFormSplit | ExpenseFormAliasSplit | null, onChange?: () => void): void => {
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
// formatted value (e.g. "10.50") doesn't fight typing while the field is focused.
const editingField = ref<string | null>(null) // id string of the field currently being edited
const editingValue = ref<string>('') // raw string shown while editing

const displayValue = (id: string, numericVal: string | number | null | undefined): string =>
  editingField.value === id
    ? editingValue.value
    : (numericVal == null ? '' : formatAmount(numericVal))

const onAmountFocus = (id: string, numericVal: string | number | null | undefined): void => {
  editingField.value = id
  editingValue.value = numericVal == null ? '' : String(numericVal)
}

const onAmountInput = (id: string, raw: string, field: string, target?: ExpenseFormSplit | ExpenseFormAliasSplit | null, onChange?: () => void): void => {
  editingValue.value = raw
  handleAmountInput(raw, field, target ?? null, onChange)
}

const onAmountBlur = (): void => {
  editingField.value = null
  editingValue.value = ''
}

const parsePercentage = (raw: string | null | undefined): number | null => {
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

const onPercentageInput = (split: ExpenseFormSplit | ExpenseFormAliasSplit, raw: string): void => {
  editingValue.value = raw
  const parsed = parsePercentage(raw)
  split.splitPercentage = parsed ?? null
}

const onPercentageBlur = (split: ExpenseFormSplit | ExpenseFormAliasSplit): void => {
  if (split.splitPercentage === null || split.splitPercentage === undefined) {
    editingField.value = null
    editingValue.value = ''
    return
  }
  if (split.splitPercentage < 0.01) split.splitPercentage = 0.01
  if (split.splitPercentage > 100) split.splitPercentage = 100
  split.splitPercentage = Math.round(split.splitPercentage * 100) / 100
  editingField.value = null
  editingValue.value = ''
}

const onModeChange = (newMode: SplitMode): void => {
  const oldMode = splitMode.value
  if (oldMode === newMode) return

  // amounts → percentage: snapshot exact amounts, convert to percentages
  if (oldMode !== 'percentage' && newMode === 'percentage') {
    exactAmountSnapshot.value = new Map()
    includedSplits.value.forEach((s) => {
      const id = isAliasMode.value ? (s as ExpenseFormAliasSplit).aliasId : (s as ExpenseFormSplit).userId
      exactAmountSnapshot.value!.set(id, toMillis(s.splitAmount ?? 0))
    })
    const amounts = includedSplits.value.map(s => toMillis(s.splitAmount ?? 0))
    const pcts = amountsToPercentages(amounts, amountMillis.value)
    includedSplits.value.forEach((s, i) => {
      s.splitPercentage = pcts[i] ?? 0
    })
  }

  // percentage → amounts: restore snapshot (lossless) or compute from percentages
  if (oldMode === 'percentage' && newMode === 'amounts') {
    if (exactAmountSnapshot.value && exactAmountSnapshot.value.size > 0) {
      includedSplits.value.forEach((s) => {
        const id = isAliasMode.value ? (s as ExpenseFormAliasSplit).aliasId : (s as ExpenseFormSplit).userId
        const snapshotted = exactAmountSnapshot.value!.get(id)
        if (snapshotted !== undefined) s.splitAmount = fromMillis(snapshotted)
      })
    }
    else {
      const pcts = includedSplits.value.map(s => s.splitPercentage ?? 0)
      const shares = distributeByPercentages(pcts, amountMillis.value)
      includedSplits.value.forEach((s, i) => {
        s.splitAmount = fromMillis(shares[i] ?? 0)
      })
    }
  }

  splitMode.value = newMode
  model.value.splitMode = newMode
}

const isLoadingMembers = ref(false)
const groupMembers = ref<GroupMember[]>([])

const splitMode = ref<SplitMode>(model.value.splitMode ?? 'amounts')
const exactAmountSnapshot = ref<Map<string, number> | null>(null)

const group = computed(() => {
  if (props.preSelectedGroupId) {
    return currentGroup.value?.id === props.preSelectedGroupId ? currentGroup.value : null
  }
  return groups.value.find(g => g.id === model.value.groupId) || null
})

const isAliasMode = computed(() => !!group.value?.useAliases)
const aliasSetupFinalized = computed(() => !!group.value?.aliasSetupFinalized)
const canCreateExpense = computed(() => !isAliasMode.value || aliasSetupFinalized.value)

const modeOptions = computed(() => [
  { value: 'amounts', label: t('expenses.splitModeAmounts') },
  { value: 'percentage', label: t('expenses.splitModePercentage') },
])

const activeSplitList = computed(() => isAliasMode.value ? (model.value.aliasSplits ?? []) : (model.value.splits ?? []))
const includedSplits = computed(() => activeSplitList.value.filter(s => s.included))

const percentageSum = computed(() =>
  includedSplits.value.reduce((sum, s) => sum + (s.splitPercentage ?? 0), 0),
)

const isPercentageBalanced = computed(() =>
  splitMode.value !== 'percentage' || Math.abs(percentageSum.value - 100) < 0.01,
)

const remainingPercentage = computed(() => 100 - percentageSum.value)

const isSingleIncluded = computed(() => includedSplits.value.length === 1)

// Enforce single-participant 100% in percentage mode — covers toggle, mode-switch, and member-load paths
watch(isSingleIncluded, (single) => {
  if (single && splitMode.value === 'percentage' && includedSplits.value.length === 1) {
    includedSplits.value[0]!.splitPercentage = 100
  }
})

const percentageAmounts = computed(() => {
  if (splitMode.value !== 'percentage' || amountMillis.value === 0) return {}
  const pcts = includedSplits.value.map(s => s.splitPercentage ?? 0)
  const shares = distributeByPercentages(pcts, amountMillis.value)
  const map: Record<string, number> = {}
  includedSplits.value.forEach((s, i) => {
    const key = isAliasMode.value ? (s as ExpenseFormAliasSplit).aliasId : (s as ExpenseFormSplit).userId
    map[key] = fromMillis(shares[i] ?? 0)
  })
  return map
})

const percentageAmountFor = (id: string): number => percentageAmounts.value[id] ?? 0

const progressColor = computed(() => {
  if (percentageSum.value > 100.01) return 'error' as const
  if (percentageSum.value < 99.99) return 'warning' as const
  return 'success' as const
})

const badgeColor = computed(() => progressColor.value)

const badgeLabel = computed(() => {
  if (percentageSum.value > 100.01) return t('expenses.overPercentage', { percentage: Math.abs(remainingPercentage.value).toFixed(2) })
  if (percentageSum.value < 99.99) return t('expenses.remainingPercentage', { percentage: Math.abs(remainingPercentage.value).toFixed(2) })
  return t('expenses.allocatedPercentage', { percentage: '100.00' })
})

const canSubmit = computed(() => {
  if (!canCreateExpense.value) return false
  if (splitMode.value === 'percentage' && !isPercentageBalanced.value) return false
  return true
})

// Tracks the last entity to manually edit a split, so "Distribute Remaining"
// can avoid re-adjusting the value they just set.
const lastModifiedEntityId = ref<string | null>(null)

// Assigns per-user shares (in millis) back onto the split records.
const assignShares = (splits: (ExpenseFormSplit | ExpenseFormAliasSplit)[], sharesMillis: number[]): void => {
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

// Select options ---------------------------------------------------------------

interface SelectOption {
  value: string | number
  label: string
}

const groupOptions = computed<SelectOption[]>(() => {
  return groups.value.map(g => ({
    value: g.id,
    label: g.name,
  }))
})

const memberOptions = computed<SelectOption[]>(() => {
  return groupMembers.value.map(member => ({
    value: member.userId,
    label: `${member.user.firstName} ${member.user.lastName}`,
  }))
})

const categoryOptions = computed<SelectOption[]>(() => {
  return categories.value.map(category => ({
    value: category.id,
    label: category.name,
  }))
})

const paymentModeOptions = computed<SelectOption[]>(() => {
  return paymentModes.value.map(mode => ({
    value: mode.id,
    label: mode.name,
  }))
})

// Split calculations -----------------------------------------------------------

// Totals in integer millicents — the source of truth for balance comparisons.
// Summing per-split millis (not millis-of-sum) avoids FP drift entirely.
const amountMillis = computed(() => toMillis(model.value.amount ?? ''))

// Clear the exact-amount snapshot when the amount changes in percentage mode,
// so the percentage→exact transition computes from percentages (not stale amounts).
watch(amountMillis, () => {
  if (splitMode.value === 'percentage') {
    exactAmountSnapshot.value = null
  }
})

const activeSplits = computed(() =>
  isAliasMode.value ? (model.value.aliasSplits ?? []) : (model.value.splits ?? []),
)

const splitTotalMillis = computed(() =>
  activeSplits.value
    .filter(s => s.included)
    .reduce((t, s) => t + toMillis(s.splitAmount ?? 0), 0),
)

const remainingMillis = computed(() => amountMillis.value - splitTotalMillis.value)

// Float projections kept for user-facing formatting only — never for comparisons.
const splitTotal = computed(() => fromMillis(splitTotalMillis.value))
const remainingAmount = computed(() => fromMillis(remainingMillis.value))

// Returns a LIVE reference into model.value.splits, creating the entry if
// missing so v-model mutations in the template are never silently lost.
const splitByUser = (userId: string): ExpenseFormSplit => {
  if (!model.value.splits) model.value.splits = []
  let s = model.value.splits.find(x => x.userId === userId)
  if (!s) {
    s = { userId, included: false, splitAmount: 0 }
    model.value.splits.push(s)
  }
  return s
}

const getSplitPercentage = (userId: string): number => {
  const split = splitByUser(userId)
  const amount = parseFloat(model.value.amount ?? '') || 0
  if (amount === 0 || !split.splitAmount) return 0
  const pct = (Number(split.splitAmount) / amount) * 100
  return parseFloat(pct.toFixed(1))
}

const trackUser = (userId: string): void => {
  lastModifiedEntityId.value = userId
}

// Split actions ----------------------------------------------------------------

// Toggle-off: rescale remaining included to keep the total (preserves ratios).
// Toggle-on:  give the new member an equal share, rescale others to fit.
const handleSplitToggle = (userId: string, included: boolean): void => {
  const split = splitByUser(userId)
  split.included = included

  if (splitMode.value === 'percentage') {
    if (!included) {
      split.splitPercentage = null
      // Rescale remaining participants proportionally to sum to 100
      const remainingSplits = includedSplits.value.filter(s => (s as ExpenseFormSplit).userId !== userId)
      if (remainingSplits.length > 0) {
        const currentSum = remainingSplits.reduce((s, x) => s + (x.splitPercentage ?? 0), 0)
        if (currentSum > 0) {
          const rescaled = remainingSplits.map(s => Math.round(((s.splitPercentage ?? 0) / currentSum) * 100 * 100) / 100)
          const rescaledSum = rescaled.reduce((a, b) => a + b, 0)
          const residual = Math.round((100 - rescaledSum) * 100) / 100
          rescaled[0] = Math.round((rescaled[0]! + residual) * 100) / 100
          remainingSplits.forEach((s, i) => {
            s.splitPercentage = rescaled[i] ?? 0
          })
        }
        else {
          const pcts = equalPercentages(remainingSplits.length)
          remainingSplits.forEach((s, i) => {
            s.splitPercentage = pcts[i] ?? 0
          })
        }
      }
    }
    else {
      const remaining = 100 - includedSplits.value.reduce((s, x) => s + (x.splitPercentage ?? 0), 0)
      if (remaining > 0) {
        split.splitPercentage = Math.max(0.01, Math.round(remaining * 100) / 100)
      }
      else {
        const n = includedSplits.value.length
        const pcts = equalPercentages(n)
        includedSplits.value.forEach((s, i) => {
          s.splitPercentage = pcts[i] ?? 0
        })
      }
    }
    return
  }

  // Existing exact-mode logic (unchanged)
  if (!model.value.amount) return
  if (!included) split.splitAmount = 0
  const includedSplitsList = model.value.splits!.filter(s => s.included)
  if (includedSplitsList.length === 0) return
  if (included) {
    const perPerson = Math.floor(amountMillis.value / includedSplitsList.length)
    split.splitAmount = fromMillis(perPerson)
    const others = includedSplitsList.filter(s => s.userId !== userId)
    if (others.length > 0) {
      const otherCurrent = others.map(s => toMillis(s.splitAmount ?? 0))
      const rescaled = redistributeMillis(otherCurrent, amountMillis.value - perPerson)
      assignShares(others, rescaled)
    }
  }
  else {
    const currentMillis = includedSplitsList.map(s => toMillis(s.splitAmount ?? 0))
    const rescaled = redistributeMillis(currentMillis, amountMillis.value)
    assignShares(includedSplitsList, rescaled)
  }
}

const splitEqually = (): void => {
  if (splitMode.value === 'percentage') {
    const splits = isAliasMode.value ? model.value.aliasSplits : model.value.splits
    if (!splits) return
    const included = splits.filter(s => s.included)
    if (included.length === 0) return
    const pcts = equalPercentages(included.length)
    included.forEach((s, i) => {
      s.splitPercentage = pcts[i] ?? 0
    })
    return
  }

  if (!model.value.amount) return

  if (isAliasMode.value) {
    if (!model.value.aliasSplits) return
    const included = model.value.aliasSplits.filter(s => s.included)
    if (included.length === 0) return
    const shares = splitMillis(amountMillis.value, included.length)
    assignShares(included, shares)
    return
  }

  if (!model.value.splits) return
  const included = model.value.splits.filter(s => s.included)
  if (included.length === 0) return
  const shares = splitMillis(amountMillis.value, included.length)
  assignShares(included, shares)
}

// Alias split helpers ----------------------------------------------------------

const splitByAlias = (aliasId: string): ExpenseFormAliasSplit => {
  if (!model.value.aliasSplits) model.value.aliasSplits = []
  let s = model.value.aliasSplits.find(x => x.aliasId === aliasId)
  if (!s) {
    s = { aliasId, included: false, splitAmount: 0 }
    model.value.aliasSplits.push(s)
  }
  return s
}

const getAliasSplitPercentage = (aliasId: string): number => {
  const split = splitByAlias(aliasId)
  const amount = parseFloat(model.value.amount ?? '') || 0
  if (amount === 0 || !split.splitAmount) return 0
  const pct = (Number(split.splitAmount) / amount) * 100
  return parseFloat(pct.toFixed(1))
}

const trackAlias = (aliasId: string): void => {
  lastModifiedEntityId.value = aliasId
}

const handleAliasSplitToggle = (aliasId: string, included: boolean): void => {
  const split = splitByAlias(aliasId)
  split.included = included

  if (splitMode.value === 'percentage') {
    if (!included) {
      split.splitPercentage = null
      // Rescale remaining participants proportionally to sum to 100
      const remainingSplits = includedSplits.value.filter(s => (s as ExpenseFormAliasSplit).aliasId !== aliasId)
      if (remainingSplits.length > 0) {
        const currentSum = remainingSplits.reduce((s, x) => s + (x.splitPercentage ?? 0), 0)
        if (currentSum > 0) {
          const rescaled = remainingSplits.map(s => Math.round(((s.splitPercentage ?? 0) / currentSum) * 100 * 100) / 100)
          const rescaledSum = rescaled.reduce((a, b) => a + b, 0)
          const residual = Math.round((100 - rescaledSum) * 100) / 100
          rescaled[0] = Math.round((rescaled[0]! + residual) * 100) / 100
          remainingSplits.forEach((s, i) => {
            s.splitPercentage = rescaled[i] ?? 0
          })
        }
        else {
          const pcts = equalPercentages(remainingSplits.length)
          remainingSplits.forEach((s, i) => {
            s.splitPercentage = pcts[i] ?? 0
          })
        }
      }
    }
    else {
      const remaining = 100 - includedSplits.value.reduce((s, x) => s + (x.splitPercentage ?? 0), 0)
      if (remaining > 0) {
        split.splitPercentage = Math.max(0.01, Math.round(remaining * 100) / 100)
      }
      else {
        const n = includedSplits.value.length
        const pcts = equalPercentages(n)
        includedSplits.value.forEach((s, i) => {
          s.splitPercentage = pcts[i] ?? 0
        })
      }
    }
    return
  }

  // Existing exact-mode logic (unchanged)
  if (!model.value.amount) return
  if (!included) split.splitAmount = 0
  const includedSplitsList = model.value.aliasSplits!.filter(s => s.included)
  if (includedSplitsList.length === 0) return
  if (included) {
    const perAlias = Math.floor(amountMillis.value / includedSplitsList.length)
    split.splitAmount = fromMillis(perAlias)
    const others = includedSplitsList.filter(s => s.aliasId !== aliasId)
    if (others.length > 0) {
      const otherCurrent = others.map(s => toMillis(s.splitAmount ?? 0))
      const rescaled = redistributeMillis(otherCurrent, amountMillis.value - perAlias)
      assignShares(others, rescaled)
    }
  }
  else {
    const currentMillis = includedSplitsList.map(s => toMillis(s.splitAmount ?? 0))
    const rescaled = redistributeMillis(currentMillis, amountMillis.value)
    assignShares(includedSplitsList, rescaled)
  }
}

// Group members ---------------------------------------------------------------

const loadGroupData = async (groupId: string): Promise<void> => {
  if (!groupId) {
    groupMembers.value = []
    return
  }

  isLoadingMembers.value = true
  try {
    const members = await fetchGroupMembers(groupId)
    groupMembers.value = members || []

    if (isAliasMode.value) {
      await fetchAliases(groupId)
    }

    if (!model.value.paidByUserId) {
      const currentUserMember = members?.find(m => m.userId === user.value?.id)
      if (currentUserMember) {
        model.value.paidByUserId = user.value!.id
      }
      else if (members && members.length > 0) {
        model.value.paidByUserId = members[0]!.userId
      }
    }
  }
  catch (error: unknown) {
    console.error('Failed to load group members:', error)
  }
  finally {
    isLoadingMembers.value = false
  }
}

watch(
  () => model.value.groupId,
  async (newGroupId: string | undefined) => {
    if (!newGroupId) return
    await fetchGroup(newGroupId)
    await loadGroupData(newGroupId)
  },
  { immediate: true },
)

// Re-sync splits with the current member/alias list whenever it changes —
// preserves existing splits, drops ones for removed entities,
// adds zero-entries for newcomers.
watch([groupMembers, aliases], (): void => {
  if (isAliasMode.value) {
    if (aliases.value && aliases.value.length > 0) updateSplits()
    return
  }
  if (groupMembers.value && groupMembers.value.length > 0) updateSplits()
}, { immediate: true })

// Validation ------------------------------------------------------------------

interface ValidationError {
  name: string
  message: string
}

const validate = (): ValidationError[] => {
  const errors: ValidationError[] = []
  if (!props.preSelectedGroupId && !model.value.groupId) {
    errors.push({ name: 'groupId', message: t('expenses.groupRequired') })
  }
  if (!model.value.title) {
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

  if (activeSplitList.value.length === 0 || activeSplitList.value.filter(s => s.included).length === 0) {
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

  if (splitMode.value === 'percentage' && !isPercentageBalanced.value) {
    errors.push({ name: 'splits', message: t('expenses.mustSumTo100') })
  }

  return errors
}

// Merge existing splits with the current member/alias list (keyed by id),
// then either initialize to an equal split (when stuck at zero) or rescale
// proportionally (when the amount changed).
const updateSplits = (): void => {
  if (isAliasMode.value) {
    updateAliasSplits()
    return
  }
  updateUserSplits()
}

const updateUserSplits = (): void => {
  const members = groupMembers.value || []

  const byId = new Map((model.value.splits || []).map(s => [s.userId, s]))
  model.value.splits = members.map(m =>
    byId.get(m.userId) ?? { userId: m.userId, included: !isEditMode.value, splitAmount: 0, splitPercentage: null },
  )

  const included = model.value.splits.filter(s => s.included)
  if (amountMillis.value === 0 || included.length === 0) return
  if (splitMode.value === 'percentage') return

  const currentTotalMillis = included.reduce(
    (s, x) => s + toMillis(x.splitAmount ?? 0),
    0,
  )

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

const updateAliasSplits = (): void => {
  const aliasList = aliases.value || []

  const byId = new Map((model.value.aliasSplits || []).map(s => [s.aliasId, s]))
  model.value.aliasSplits = aliasList.map(a =>
    byId.get(a.id) ?? { aliasId: a.id, included: !isEditMode.value, splitAmount: 0, splitPercentage: null },
  )

  const included = model.value.aliasSplits.filter(s => s.included)
  if (amountMillis.value === 0 || included.length === 0) return
  if (splitMode.value === 'percentage') return

  const currentTotalMillis = included.reduce(
    (s, x) => s + toMillis(x.splitAmount ?? 0),
    0,
  )

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

// Submit ----------------------------------------------------------------------

const onSubmit = async (): Promise<void> => {
  emit('submit', buildExpensePayload())
}

const effectiveGroupId = computed(() => props.preSelectedGroupId || model.value.groupId)

const buildExpensePayload = (): CreateExpensePayload => {
  const payload: CreateExpensePayload = {
    groupId: effectiveGroupId.value!,
    expenseData: {
      title: model.value.title ?? null,
      description: model.value.description || null,
      amount: parseFloat(model.value.amount ?? ''),
      paidByUserId: model.value.paidByUserId ?? null,
      expenseDate: model.value.expenseDate ?? null,
      categoryId: model.value.categoryId || undefined,
      paymentModeId: model.value.paymentModeId || undefined,
    },
  }

  if (isAliasMode.value) {
    const splits = model.value.aliasSplits?.filter(s => s.included) ?? []
    if (splitMode.value === 'percentage') {
      const pcts = splits.map(s => s.splitPercentage ?? 0)
      const shares = distributeByPercentages(pcts, amountMillis.value)
      payload.expenseData.aliasSplits = splits
        .map((s, i) => ({ aliasId: s.aliasId, splitAmount: fromMillis(shares[i] ?? 0) }))
        .filter(s => s.splitAmount > 0)
    }
    else {
      payload.expenseData.aliasSplits = splits
        .map(s => ({ aliasId: s.aliasId, splitAmount: parseFloat(String(s.splitAmount)) || 0 }))
        .filter(s => s.splitAmount > 0)
    }
  }
  else {
    const splits = model.value.splits?.filter(s => s.included) ?? []
    if (splitMode.value === 'percentage') {
      const pcts = splits.map(s => s.splitPercentage ?? 0)
      const shares = distributeByPercentages(pcts, amountMillis.value)
      payload.expenseData.splits = splits
        .map((s, i) => ({ userId: s.userId, splitAmount: fromMillis(shares[i] ?? 0) }))
        .filter(s => s.splitAmount > 0)
    }
    else {
      payload.expenseData.splits = splits
        .map(s => ({ userId: s.userId, splitAmount: parseFloat(String(s.splitAmount)) || 0 }))
        .filter(s => s.splitAmount > 0)
    }
  }

  return payload
}

const onAddMore = (): void => {
  const errors = validate()
  if (errors.length > 0) return
  emit('addMore', buildExpensePayload())
}

const addMoreMenuItems = computed(() => [[
  {
    label: t('expenses.addAndAddMore'),
    icon: 'i-lucide-plus',
    onSelect: onAddMore,
  },
]])

const goBack = (): void => {
  emit('cancel')
}

onMounted(async () => {
  try {
    await fetchGroups()
  }
  catch (error: unknown) {
    console.error('Failed to load form data:', error)
  }
})
</script>
