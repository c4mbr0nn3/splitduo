<template>
  <div class="flex flex-col items-center justify-center p-4">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <div class="flex items-center justify-between">
          <UiCardHeader :title="title" />
          <UButton
            v-if="receiptImageUrl"
            icon="i-lucide-image"
            label="View Receipt"
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
        <UFormField
          v-if="!preSelectedGroupId"
          label="Group"
          name="groupId"
          required
        >
          <USelect
            v-model="model.groupId"
            :items="groupOptions"
            placeholder="Select a group"
            size="lg"
            :loading="isLoadingGroups"
            class="w-full"
          />
        </UFormField>
        <UFormField
          label="Expense Title"
          name="title"
          required
        >
          <UInput
            v-model="model.title"
            placeholder="Enter expense title"
            size="lg"
            class="w-full"
            maxlength="255"
          />
        </UFormField>
        <UFormField
          label="Amount"
          name="amount"
          required
        >
          <UInputNumber
            v-model="model.amount"
            :step="0.01"
            :min="0.01"
            orientation="horizontal"
            placeholder="Enter the amount"
            size="lg"
            class="w-full"
            @update:model-value="updateSplits"
          />
        </UFormField>
        <UFormField
          label="Description"
          name="description"
        >
          <UTextarea
            v-model="model.description"
            placeholder="Enter description (optional)"
            size="lg"
            class="w-full"
          />
        </UFormField>
        <UFormField
          label="Who Paid?"
          name="paidByUserId"
          required
        >
          <USelect
            v-model="model.paidByUserId"
            :items="memberOptions"
            placeholder="Select who paid"
            size="lg"
            :disabled="!model.groupId"
            :loading="isLoadingMembers"
            class="w-full"
          />
        </UFormField>
        <UFormField
          label="Date"
          name="expenseDate"
          required
        >
          <UiInputDate
            v-model="model.expenseDate"
            size="lg"
          />
        </UFormField>
        <UFormField
          label="Category"
          name="categoryId"
          required
        >
          <USelect
            v-model="model.categoryId"
            :items="categoryOptions"
            placeholder="Select category"
            size="lg"
            :loading="isLoadingCategories"
            class="w-full"
          />
        </UFormField>
        <UFormField
          label="Payment Method"
          name="paymentModeId"
          required
        >
          <USelect
            v-model="model.paymentModeId"
            :items="paymentModeOptions"
            placeholder="Select payment method"
            size="lg"
            :loading="isLoadingPaymentModes"
            class="w-full"
          />
        </UFormField>

        <!-- Split Section -->
        <div class="space-y-2">
          <div class="flex items-center justify-between">
            <label class="block text-sm font-medium py-2">
              Split Between
            </label>
            <UiButtonDropdown
              label="Adjust Splits"
              :items="adjustSplitsMenuItems"
              size="sm"
              variant="soft"
              color="primary"
              :disabled="!model.amount || !groupMembers.length"
            />
          </div>
          <div class="space-y-3">
            <template
              v-for="member in groupMembers"
              :key="member.userId"
            >
              <UCard :variant="splitByUser(member.userId).included ? 'subtle' : 'outline'">
                <template #header>
                  <div class="flex items-center justify-between">
                    <span class="flex items-center gap-2 font-medium text-sm">
                      {{ member.user.firstName }} {{ member.user.lastName }}
                      <span
                        v-if="model.amount && splitByUser(member.userId).splitAmount > 0"
                        class="text-xs text-muted"
                      >
                        ({{ getSplitPercentage(member.userId) }}%)
                      </span>
                    </span>
                    <UCheckbox
                      v-model="splitByUser(member.userId).included"
                      :color="splitByUser(member.userId).included ? 'success' : 'default'"
                      @update:model-value="(value) => handleSplitToggle(member.userId, value)"
                    />
                  </div>
                </template>
                <div class="space-y-1">
                  <UInputNumber
                    v-model="splitByUser(member.userId).splitAmount"
                    :step="0.001"
                    :min="0"
                    placeholder="Amount"
                    :variant="splitByUser(member.userId).included ? 'subtle' : 'ghost'"
                    :disabled="!splitByUser(member.userId).included"
                    :color="getSplitValidationState(member.userId)?.state === 'error' ? 'error' : undefined"
                    class="w-full"
                    @update:model-value="trackUser(member.userId)"
                  />
                  <span
                    v-if="getSplitValidationState(member.userId)?.state === 'error'"
                    class="text-xs text-red-500"
                  >
                    {{ getSplitValidationState(member.userId)?.message }}
                  </span>
                </div>
              </UCard>
            </template>
          </div>
          <div class="text-xs space-y-1">
            <div
              v-if="model.amount"
              class="flex justify-between items-center"
            >
              <span class="text-toned">Expense Total:</span>
              <span class="font-medium">{{ formatCurrency(parseFloat(model.amount)) }}</span>
            </div>
            <div
              v-if="splitTotal > 0"
              class="flex justify-between items-center"
            >
              <span class="text-toned">Split Total:</span>
              <span class="font-medium">{{ formatCurrency(splitTotal) }}</span>
            </div>
            <div
              v-if="model.amount && remainingMillis !== 0"
              class="flex justify-between items-center font-semibold"
              :class="{
                'text-red-500': remainingMillis < 0,
                'text-orange-500': remainingMillis > 0,
              }"
            >
              <span>{{ remainingMillis > 0 ? 'Remaining:' : 'Over by:' }}</span>
              <span>{{ formatCurrency(Math.abs(remainingAmount)) }}</span>
            </div>
            <div
              v-if="model.amount && remainingMillis === 0"
              class="flex justify-between items-center text-green-600 font-semibold"
            >
              <span>✓ Splits balanced</span>
            </div>
          </div>
        </div>

        <div class="flex justify-between items-center pt-4">
          <UButton
            type="button"
            label="Cancel"
            variant="outline"
            size="lg"
            @click="goBack"
          />
          <UFieldGroup size="lg">
            <UButton
              type="submit"
              variant="subtle"
              :label="submitLabel"
              :loading="loading"
            />
            <UDropdownMenu
              v-if="showAddMore"
              :items="addMoreMenuItems"
            >
              <UButton
                type="button"
                variant="outline"
                icon="i-lucide-chevron-down"
                :loading="loading"
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

const { user } = useAuth()
const { groups, fetchGroups, fetchGroupMembers, isLoading: isLoadingGroups } = useGroups()
const { categories, isLoading: isLoadingCategories } = useCategories()
const { paymentModes, isLoading: isLoadingPaymentModes } = usePaymentModes()
const { showError } = useNotifications()

const isLoadingMembers = ref(false)
const groupMembers = ref([])

// Tracks the last user to manually edit a split, so "Distribute Remaining"
// can avoid re-adjusting the value they just set.
const lastModifiedUserId = ref(null)

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

const splitTotalMillis = computed(() =>
  (model.value.splits ?? [])
    .filter(s => s.included)
    .reduce((t, s) => t + toMillis(s.splitAmount), 0),
)

const remainingMillis = computed(() => amountMillis.value - splitTotalMillis.value)

// Float projections kept for user-facing formatting only — never for comparisons.
const splitTotal = computed(() => fromMillis(splitTotalMillis.value))
const remainingAmount = computed(() => fromMillis(remainingMillis.value))

const showDistributeButton = computed(() => {
  if (!model.value.amount) return false
  return remainingMillis.value !== 0
})

const areSplitsEqual = computed(() => {
  if (!model.value.amount || !model.value.splits) return true
  const includedSplits = model.value.splits.filter(s => s.included)
  if (includedSplits.length === 0) return true

  const expected = splitMillis(amountMillis.value, includedSplits.length)
  const current = includedSplits.map(s => toMillis(s.splitAmount))
  // Sort both so we compare multisets — fair split positions are interchangeable.
  expected.sort((a, b) => a - b)
  current.sort((a, b) => a - b)
  return current.every((m, i) => m === expected[i])
})

const adjustSplitsMenuItems = computed(() => {
  return [
    {
      label: 'Distribute Remaining',
      icon: 'i-heroicons-arrows-right-left',
      onSelect: distributeRemaining,
      visible: showDistributeButton.value,
    },
    {
      label: 'Split Equally',
      icon: 'i-heroicons-equals',
      onSelect: splitEqually,
      visible: !areSplitsEqual.value,
    },
  ]
})

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
  lastModifiedUserId.value = userId
}

const getSplitValidationState = (userId) => {
  const split = splitByUser(userId)
  if (!split.included) return null

  const amount = parseFloat(model.value.amount) || 0
  if (amount === 0) return null

  if (split.splitAmount <= 0) {
    return { state: 'error', message: 'Amount must be greater than zero' }
  }
  if (split.splitAmount > amount) {
    return { state: 'error', message: 'Amount exceeds total expense' }
  }
  return null
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
  if (!model.value.amount || !model.value.splits) return
  const includedSplits = model.value.splits.filter(s => s.included)
  if (includedSplits.length === 0) return

  const shares = splitMillis(amountMillis.value, includedSplits.length)
  assignShares(includedSplits, shares)
}

// Distributes the delta fairly among included members (minus last-modified).
// Aborts if any recipient would go negative, instead of silently clamping.
const distributeRemaining = () => {
  if (!model.value.amount || !model.value.splits) return

  const deltaMillis = remainingMillis.value
  if (deltaMillis === 0) return

  const targets = model.value.splits.filter(s =>
    s.included && s.userId !== lastModifiedUserId.value,
  )
  if (targets.length === 0) return

  const sign = deltaMillis >= 0 ? 1 : -1
  const adjustments = splitMillis(Math.abs(deltaMillis), targets.length)

  const wouldGoNegative = targets.some((s, i) =>
    toMillis(s.splitAmount) + sign * adjustments[i] < 0,
  )
  if (wouldGoNegative) {
    showError('Cannot distribute: some splits would go negative')
    return
  }

  targets.forEach((s, i) => {
    s.splitAmount = fromMillis(toMillis(s.splitAmount) + sign * adjustments[i])
  })
  lastModifiedUserId.value = null
}

// Group members ---------------------------------------------------------------

const loadGroupMembers = async (groupId) => {
  if (!groupId) {
    groupMembers.value = []
    return
  }

  isLoadingMembers.value = true
  try {
    const members = await fetchGroupMembers(groupId)
    groupMembers.value = members || []

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
  (newGroupId) => {
    if (newGroupId) loadGroupMembers(newGroupId)
  },
  { immediate: true },
)

// Re-sync splits with the current member list whenever it changes —
// preserves existing per-user splits, drops ones for removed members,
// adds zero-entries for newcomers.
watch(groupMembers, (members) => {
  if (members && members.length > 0) updateSplits()
})

// Validation ------------------------------------------------------------------

const validate = () => {
  const errors = []
  if (!props.preSelectedGroupId && !model.value.groupId) {
    errors.push({ name: 'groupId', message: 'Group is required' })
  }
  if (!model.value.title) {
    errors.push({ name: 'title', message: 'Title is required' })
  }
  if (!model.value.amount) {
    errors.push({ name: 'amount', message: 'Amount is required' })
  }
  if (!model.value.paidByUserId) {
    errors.push({ name: 'paidByUserId', message: 'Paid By is required' })
  }
  if (!model.value.categoryId) {
    errors.push({ name: 'categoryId', message: 'Category is required' })
  }
  if (!model.value.paymentModeId) {
    errors.push({ name: 'paymentModeId', message: 'Payment Mode is required' })
  }

  if (!model.value.splits || model.value.splits.filter(s => s.included).length === 0) {
    errors.push({ name: 'splits', message: 'At least one person must be included in the split' })
  }
  else if (model.value.amount && remainingMillis.value !== 0) {
    errors.push({
      name: 'splits',
      message: `Split total (${formatCurrency(splitTotal.value)}) must equal expense amount (${formatCurrency(parseFloat(model.value.amount))})`,
    })
  }

  return errors
}

// Merge existing splits with the current member list (keyed by userId),
// then either initialize to an equal split (when stuck at zero) or rescale
// proportionally (when the amount changed).
const updateSplits = () => {
  const members = groupMembers.value || []

  const byId = new Map((model.value.splits || []).map(s => [s.userId, s]))
  model.value.splits = members.map(m =>
    byId.get(m.userId) ?? { userId: m.userId, included: true, splitAmount: 0 },
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

// Submit ----------------------------------------------------------------------

const onSubmit = async () => {
  emit('submit', buildExpensePayload())
}

const buildExpensePayload = () => {
  return {
    groupId: props.preSelectedGroupId || model.value.groupId,
    expenseData: {
      title: model.value.title,
      description: model.value.description || null,
      amount: parseFloat(model.value.amount),
      paidByUserId: model.value.paidByUserId,
      expenseDate: model.value.expenseDate,
      categoryId: model.value.categoryId || undefined,
      paymentModeId: model.value.paymentModeId || undefined,
      splits: model.value.splits
        ? model.value.splits.filter(s => s.included).map(s => ({
            userId: s.userId,
            splitAmount: parseFloat(s.splitAmount) || 0,
          }))
        : [],
    },
  }
}

const onAddMore = () => {
  const errors = validate()
  if (errors.length > 0) return
  emit('addMore', buildExpensePayload())
}

const addMoreMenuItems = computed(() => [[
  {
    label: 'Add & Add More',
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
