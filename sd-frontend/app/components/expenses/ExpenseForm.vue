<template>
  <div class="flex flex-col items-center justify-center py-6 sm:py-8 px-4">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <div class="flex items-center justify-between">
          <UiCardHeader :title="title" />
          <UButton
            v-if="receiptImageUrl"
            icon="i-lucide-image"
            :label="$t('expenses.viewReceipt')"
            size="sm"
            variant="ghost"
            @click="isReceiptPreviewOpen = true"
          />
        </div>
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
          <div class="flex items-center justify-between mb-3">
            <p class="text-sm font-medium text-muted">
              {{ isAliasMode ? $t('expenses.splitBetweenAliases') : $t('expenses.splitBetween') }}
            </p>
            <div class="flex gap-2">
              <UButton
                :label="$t('expenses.splitEqually')"
                variant="ghost"
                color="neutral"
                size="xs"
                @click="splitEqually"
              />
              <UButton
                :label="$t('expenses.adjustSplits')"
                variant="ghost"
                color="neutral"
                size="xs"
                @click="showAdvanced = !showAdvanced"
              />
            </div>
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
                <span class="text-xs text-muted min-w-[2.5rem] text-right">
                  {{ model.amount && splitByAlias(alias.id).splitAmount > 0 ? `${getAliasSplitPercentage(alias.id)}%` : '' }}
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
                  <UAvatar
                    icon="i-lucide-user"
                    size="sm"
                    :class="splitByUser(member.userId).included ? 'ring-2 ring-primary bg-primary/10 text-primary' : 'bg-muted/10 text-muted opacity-60'"
                    :alt="`${member.user.firstName} ${member.user.lastName}`"
                  />
                  <span
                    class="text-sm truncate"
                    :class="splitByUser(member.userId).included ? 'text-highlighted' : 'text-muted'"
                  >
                    {{ member.user.firstName }} {{ member.user.lastName }}
                  </span>
                </button>
                <span class="text-xs text-muted min-w-[2.5rem] text-right">
                  {{ model.amount && splitByUser(member.userId).splitAmount > 0 ? `${getSplitPercentage(member.userId)}%` : '' }}
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
              </div>
            </template>
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
            @click="goBack"
          />
          <UFieldGroup size="lg">
            <UButton
              type="submit"
              variant="subtle"
              :label="submitLabel"
              :loading="loading"
              :disabled="!canCreateExpense"
            />
            <UDropdownMenu
              v-if="showAddMore"
              :items="addMoreMenuItems"
            >
              <UButton
                type="button"
                variant="subtle"
                icon="i-lucide-chevron-down"
                :loading="loading"
                :disabled="!canCreateExpense"
              />
            </UDropdownMenu>
          </UFieldGroup>
        </div>
      </UForm>
    </UCard>
    <UiReceiptPreviewModal
      v-model="isReceiptPreviewOpen"
      :image-url="receiptImageUrl"
    />
  </div>
</template>

<script setup>
const { t } = useI18n()

const props = defineProps({
  title: {
    type: String,
    required: true,
  },
  submitLabel: {
    type: String,
    required: true,
  },
  loading: {
    type: Boolean,
    default: false,
  },
  preSelectedGroupId: {
    type: String,
    default: null,
  },
  showAddMore: {
    type: Boolean,
    default: false,
  },
})

const model = defineModel({
  type: Object,
  default: () => ({}),
})

const emit = defineEmits(['submit', 'addMore', 'cancel'])

const { receiptImageUrl } = useReceiptScan()
const isReceiptPreviewOpen = ref(false)
const isEditMode = computed(() => !!model.value?.expenseId)

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
const parseAmount = (raw) => {
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

const handleAmountInput = (raw, field, target, onChange) => {
  const parsed = parseAmount(raw)

  if (target) {
    target[field] = parsed ?? null
  }
  else {
    model.value[field] = parsed ?? null
    updateSplits()
  }

  onChange?.()
}

// Display formatting is applied on blur, not on every keystroke, so the
// formatted value (e.g. "10.50") doesn't fight typing while the field is focused.
const editingField = ref(null) // id string of the field currently being edited
const editingValue = ref('') // raw string shown while editing

const displayValue = (id, numericVal) =>
  editingField.value === id
    ? editingValue.value
    : (numericVal == null ? '' : formatAmount(numericVal))

const onAmountFocus = (id, numericVal) => {
  editingField.value = id
  editingValue.value = numericVal == null ? '' : String(numericVal)
}

const onAmountInput = (id, raw, field, target, onChange) => {
  editingValue.value = raw
  handleAmountInput(raw, field, target, onChange)
}

const onAmountBlur = () => {
  editingField.value = null
  editingValue.value = ''
}

const isLoadingMembers = ref(false)
const groupMembers = ref([])
const showAdvanced = ref(false)

const group = computed(() => {
  if (props.preSelectedGroupId) {
    return currentGroup.value?.id === props.preSelectedGroupId ? currentGroup.value : null
  }
  return groups.value.find(g => g.id === model.value.groupId) || null
})

const isAliasMode = computed(() => !!group.value?.useAliases)
const aliasSetupFinalized = computed(() => !!group.value?.aliasSetupFinalized)
const canCreateExpense = computed(() => !isAliasMode.value || aliasSetupFinalized.value)

// Tracks the last entity to manually edit a split, so "Distribute Remaining"
// can avoid re-adjusting the value they just set.
const lastModifiedEntityId = ref(null)

// Assigns per-user shares (in millis) back onto the split records.
const assignShares = (splits, sharesMillis) => {
  splits.forEach((s, i) => {
    s.splitAmount = fromMillis(sharesMillis[i] ?? 0)
  })
}

// Rescale to target, but re-equalize if the current values were a fair-remainder
// equal split (differ by ≤1 millicent) — otherwise proportional scaling would
// turn [23.334, 23.333] into [35.001, 34.999] instead of [35, 35].
const redistributeMillis = (currentMillis, targetMillis) => {
  if (currentMillis.length === 0) return []
  const spread = Math.max(...currentMillis) - Math.min(...currentMillis)
  return spread <= 1
    ? splitMillis(targetMillis, currentMillis.length)
    : rescaleMillis(currentMillis, targetMillis)
}

// Select options ---------------------------------------------------------------

const groupOptions = computed(() => {
  return groups.value.map(g => ({
    value: g.id,
    label: g.name,
  }))
})

const memberOptions = computed(() => {
  return groupMembers.value.map(member => ({
    value: member.userId,
    label: `${member.user.firstName} ${member.user.lastName}`,
  }))
})

const categoryOptions = computed(() => {
  return categories.value.map(category => ({
    value: category.id,
    label: category.name,
  }))
})

const paymentModeOptions = computed(() => {
  return paymentModes.value.map(mode => ({
    value: mode.id,
    label: mode.name,
  }))
})

// Split calculations -----------------------------------------------------------

// Totals in integer millicents — the source of truth for balance comparisons.
// Summing per-split millis (not millis-of-sum) avoids FP drift entirely.
const amountMillis = computed(() => toMillis(model.value.amount))

const activeSplits = computed(() =>
  isAliasMode.value ? (model.value.aliasSplits ?? []) : (model.value.splits ?? []),
)

const splitTotalMillis = computed(() =>
  activeSplits.value
    .filter(s => s.included)
    .reduce((t, s) => t + toMillis(s.splitAmount), 0),
)

const remainingMillis = computed(() => amountMillis.value - splitTotalMillis.value)

// Float projections kept for user-facing formatting only — never for comparisons.
const splitTotal = computed(() => fromMillis(splitTotalMillis.value))
const remainingAmount = computed(() => fromMillis(remainingMillis.value))

// Returns a LIVE reference into model.value.splits, creating the entry if
// missing so v-model mutations in the template are never silently lost.
const splitByUser = (userId) => {
  if (!model.value.splits) model.value.splits = []
  let s = model.value.splits.find(x => x.userId === userId)
  if (!s) {
    s = { userId, included: false, splitAmount: 0 }
    model.value.splits.push(s)
  }
  return s
}

const getSplitPercentage = (userId) => {
  const split = splitByUser(userId)
  const amount = parseFloat(model.value.amount) || 0
  if (amount === 0 || !split.splitAmount) return 0
  const pct = (split.splitAmount / amount) * 100
  return parseFloat(pct.toFixed(1))
}

const trackUser = (userId) => {
  lastModifiedEntityId.value = userId
}

// Split actions ----------------------------------------------------------------

// Toggle-off: rescale remaining included to keep the total (preserves ratios).
// Toggle-on:  give the new member an equal share, rescale others to fit.
const handleSplitToggle = (userId, included) => {
  const split = splitByUser(userId)
  split.included = included
  if (!model.value.amount) return

  if (!included) split.splitAmount = 0

  const includedSplits = model.value.splits.filter(s => s.included)
  if (includedSplits.length === 0) return

  if (included) {
    const perPerson = Math.floor(amountMillis.value / includedSplits.length)
    split.splitAmount = fromMillis(perPerson)
    const others = includedSplits.filter(s => s.userId !== userId)
    if (others.length > 0) {
      const otherCurrent = others.map(s => toMillis(s.splitAmount))
      const rescaled = redistributeMillis(otherCurrent, amountMillis.value - perPerson)
      assignShares(others, rescaled)
    }
  }
  else {
    const currentMillis = includedSplits.map(s => toMillis(s.splitAmount))
    const rescaled = redistributeMillis(currentMillis, amountMillis.value)
    assignShares(includedSplits, rescaled)
  }
}

const splitEqually = () => {
  if (!model.value.amount) return

  if (isAliasMode.value) {
    if (!model.value.aliasSplits) return
    const includedSplits = model.value.aliasSplits.filter(s => s.included)
    if (includedSplits.length === 0) return
    const shares = splitMillis(amountMillis.value, includedSplits.length)
    assignShares(includedSplits, shares)
    return
  }

  if (!model.value.splits) return
  const includedSplits = model.value.splits.filter(s => s.included)
  if (includedSplits.length === 0) return
  const shares = splitMillis(amountMillis.value, includedSplits.length)
  assignShares(includedSplits, shares)
}

// Alias split helpers ----------------------------------------------------------

const splitByAlias = (aliasId) => {
  if (!model.value.aliasSplits) model.value.aliasSplits = []
  let s = model.value.aliasSplits.find(x => x.aliasId === aliasId)
  if (!s) {
    s = { aliasId, included: false, splitAmount: 0 }
    model.value.aliasSplits.push(s)
  }
  return s
}

const getAliasSplitPercentage = (aliasId) => {
  const split = splitByAlias(aliasId)
  const amount = parseFloat(model.value.amount) || 0
  if (amount === 0 || !split.splitAmount) return 0
  const pct = (split.splitAmount / amount) * 100
  return parseFloat(pct.toFixed(1))
}

const trackAlias = (aliasId) => {
  lastModifiedEntityId.value = aliasId
}

const handleAliasSplitToggle = (aliasId, included) => {
  const split = splitByAlias(aliasId)
  split.included = included
  if (!model.value.amount) return

  if (!included) split.splitAmount = 0

  const includedSplits = model.value.aliasSplits.filter(s => s.included)
  if (includedSplits.length === 0) return

  if (included) {
    const perAlias = Math.floor(amountMillis.value / includedSplits.length)
    split.splitAmount = fromMillis(perAlias)
    const others = includedSplits.filter(s => s.aliasId !== aliasId)
    if (others.length > 0) {
      const otherCurrent = others.map(s => toMillis(s.splitAmount))
      const rescaled = redistributeMillis(otherCurrent, amountMillis.value - perAlias)
      assignShares(others, rescaled)
    }
  }
  else {
    const currentMillis = includedSplits.map(s => toMillis(s.splitAmount))
    const rescaled = redistributeMillis(currentMillis, amountMillis.value)
    assignShares(includedSplits, rescaled)
  }
}

// Group members ---------------------------------------------------------------

const loadGroupData = async (groupId) => {
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
        model.value.paidByUserId = user.value.id
      }
      else if (members?.length > 0) {
        model.value.paidByUserId = members[0].userId
      }
    }
  }
  catch (error) {
    console.error('Failed to load group members:', error)
  }
  finally {
    isLoadingMembers.value = false
  }
}

watch(
  () => model.value.groupId,
  async (newGroupId) => {
    if (!newGroupId) return
    await fetchGroup(newGroupId)
    await loadGroupData(newGroupId)
  },
  { immediate: true },
)

// Re-sync splits with the current member/alias list whenever it changes —
// preserves existing splits, drops ones for removed entities,
// adds zero-entries for newcomers.
watch([groupMembers, aliases], () => {
  if (isAliasMode.value) {
    if (aliases.value && aliases.value.length > 0) updateSplits()
    return
  }
  if (groupMembers.value && groupMembers.value.length > 0) updateSplits()
}, { immediate: true })

// Validation ------------------------------------------------------------------

const validate = () => {
  const errors = []
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

  const activeSplitList = isAliasMode.value ? model.value.aliasSplits : model.value.splits
  const splitEntityLabel = isAliasMode.value ? t('expenses.alias') : t('expenses.person')

  if (!activeSplitList || activeSplitList.filter(s => s.included).length === 0) {
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

  return errors
}

// Merge existing splits with the current member/alias list (keyed by id),
// then either initialize to an equal split (when stuck at zero) or rescale
// proportionally (when the amount changed).
const updateSplits = () => {
  if (isAliasMode.value) {
    updateAliasSplits()
    return
  }
  updateUserSplits()
}

const updateUserSplits = () => {
  const members = groupMembers.value || []

  const byId = new Map((model.value.splits || []).map(s => [s.userId, s]))
  model.value.splits = members.map(m =>
    byId.get(m.userId) ?? { userId: m.userId, included: !isEditMode.value, splitAmount: 0 },
  )

  const includedSplits = model.value.splits.filter(s => s.included)
  if (amountMillis.value === 0 || includedSplits.length === 0) return

  const currentTotalMillis = includedSplits.reduce(
    (s, x) => s + toMillis(x.splitAmount),
    0,
  )

  if (currentTotalMillis === 0) {
    assignShares(includedSplits, splitMillis(amountMillis.value, includedSplits.length))
    return
  }

  if (currentTotalMillis !== amountMillis.value) {
    const rescaled = redistributeMillis(
      includedSplits.map(s => toMillis(s.splitAmount)),
      amountMillis.value,
    )
    assignShares(includedSplits, rescaled)
  }
}

const updateAliasSplits = () => {
  const aliasList = aliases.value || []

  const byId = new Map((model.value.aliasSplits || []).map(s => [s.aliasId, s]))
  model.value.aliasSplits = aliasList.map(a =>
    byId.get(a.id) ?? { aliasId: a.id, included: !isEditMode.value, splitAmount: 0 },
  )

  const includedSplits = model.value.aliasSplits.filter(s => s.included)
  if (amountMillis.value === 0 || includedSplits.length === 0) return

  const currentTotalMillis = includedSplits.reduce(
    (s, x) => s + toMillis(x.splitAmount),
    0,
  )

  if (currentTotalMillis === 0) {
    assignShares(includedSplits, splitMillis(amountMillis.value, includedSplits.length))
    return
  }

  if (currentTotalMillis !== amountMillis.value) {
    const rescaled = redistributeMillis(
      includedSplits.map(s => toMillis(s.splitAmount)),
      amountMillis.value,
    )
    assignShares(includedSplits, rescaled)
  }
}

// Submit ----------------------------------------------------------------------

const onSubmit = async () => {
  emit('submit', buildExpensePayload())
}

const effectiveGroupId = computed(() => props.preSelectedGroupId || model.value.groupId)

const buildExpensePayload = () => {
  const payload = {
    groupId: effectiveGroupId.value,
    expenseData: {
      title: model.value.title,
      description: model.value.description || null,
      amount: parseFloat(model.value.amount),
      paidByUserId: model.value.paidByUserId,
      expenseDate: model.value.expenseDate,
      categoryId: model.value.categoryId || undefined,
      paymentModeId: model.value.paymentModeId || undefined,
    },
  }

  if (isAliasMode.value) {
    payload.expenseData.aliasSplits = model.value.aliasSplits
      ? model.value.aliasSplits.filter(s => s.included).map(s => ({
          aliasId: s.aliasId,
          splitAmount: parseFloat(s.splitAmount) || 0,
        }))
      : []
  }
  else {
    payload.expenseData.splits = model.value.splits
      ? model.value.splits.filter(s => s.included).map(s => ({
          userId: s.userId,
          splitAmount: parseFloat(s.splitAmount) || 0,
        }))
      : []
  }

  return payload
}

const onAddMore = () => {
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

const goBack = () => {
  emit('cancel')
}

onMounted(async () => {
  try {
    await fetchGroups()
  }
  catch (error) {
    console.error('Failed to load form data:', error)
  }
})
</script>
