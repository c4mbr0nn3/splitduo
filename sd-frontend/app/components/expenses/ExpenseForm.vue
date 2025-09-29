<template>
  <div class="flex flex-col items-center justify-center p-4">
    <UCard class="w-full max-w-lg">
      <template #header>
        <h2 class="text-xl font-bold text-center">
          {{ title }}
        </h2>
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
          <UInput
            v-model="model.expenseDate"
            type="date"
            size="lg"
            class="w-full"
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
          <label class="block text-sm font-medium">
            Split Between
          </label>
          <div class="space-y-3">
            <template
              v-for="member in groupMembers"
              :key="member.userId"
            >
              <UCard :variant="splitByUser(member.userId).included ? 'subtle' : 'outline'">
                <template #header>
                  <div class="flex items-center justify-between">
                    <span class="font-medium text-sm">
                      {{ member.user.firstName }} {{ member.user.lastName }}
                    </span>
                    <UCheckbox
                      v-model="splitByUser(member.userId).included"
                      :color="splitByUser(member.userId).included ? 'success' : 'default'"
                    />
                  </div>
                </template>
                <UInputNumber
                  v-model="splitByUser(member.userId).splitAmount"
                  :step="0.001"
                  :min="0"
                  placeholder="Amount"
                  :variant="splitByUser(member.userId).included ? 'subtle' : 'ghost'"
                  :disabled="!splitByUser(member.userId).included"
                  class="w-full"
                />
              </UCard>
            </template>
          </div>
          <div class="text-xs text-muted">
            <span v-if="splitTotal > 0">
              Split total: {{ formatCurrency(splitTotal) }}
              <span
                v-if="model.amount && splitTotal !== parseFloat(model.amount)"
                class="text-orange-500"
              >
                ({{ splitTotal > parseFloat(model.amount) ? 'Over' : 'Under' }} by {{ formatCurrency(Math.abs(splitTotal - parseFloat(model.amount))) }})
              </span>
            </span>
          </div>
        </div>

        <div class="flex space-x-3 pt-4">
          <UButton
            type="button"
            label="Cancel"
            variant="outline"
            size="lg"
            class="flex-1"
            @click="goBack"
          />
          <UButton
            type="submit"
            :label="submitLabel"
            size="lg"
            class="flex-1"
            :loading="loading"
          />
        </div>
      </UForm>
    </UCard>
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
})

const model = defineModel({
  type: Object,
  default: () => ({}),
})

const emit = defineEmits(['submit', 'cancel'])

const { user } = useAuth()
const { groups, fetchGroups, fetchGroupMembers, isLoading: isLoadingGroups } = useGroups()
const { categories, isLoading: isLoadingCategories } = useCategories()
const { paymentModes, isLoading: isLoadingPaymentModes } = usePaymentModes()

// Loading states
const isLoadingMembers = ref(false)

// Group members
const groupMembers = ref([])

// Computed options for selects
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

// Split calculations
const splitTotal = computed(() => {
  return model.value.splits
    ? model.value.splits
        .filter(s => s.included)
        .reduce((total, s) => total + (parseFloat(s.splitAmount) || 0), 0)
    : 0
})

const splitByUser = (userId) => {
  return model.value.splits.find(s => s.userId === userId) || { userId: userId, included: false, splitAmount: 0 }
}

const formatCurrency = (amount) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(amount)
}

// Load group members when group changes
const loadGroupMembers = async (groupId) => {
  if (!groupId) {
    groupMembers.value = []
    return
  }

  isLoadingMembers.value = true
  try {
    const members = await fetchGroupMembers(groupId)
    groupMembers.value = members || []

    // Auto-select current user if they're in the group and no one is selected yet
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

// Watch for group changes
watch(
  () => model.value.groupId,
  (newGroupId) => {
    if (newGroupId) {
      loadGroupMembers(newGroupId)
    }
  },
  { immediate: true },
)

// Form validation
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

  // Validate splits
  if (!model.value.splits || model.value.splits.filter(s => s.included).length === 0) {
    errors.push({ name: 'splits', message: 'At least one person must be included in the split' })
  }
  else if (model.value.amount) {
    const difference = Math.abs(splitTotal.value - parseFloat(model.value.amount))
    if (difference > 0.001) {
      errors.push({
        name: 'splits',
        message: `Split total (${formatCurrency(splitTotal.value)}) must equal expense amount (${formatCurrency(parseFloat(model.value.amount))})`
      })
    }
  }

  return errors
}

const updateSplits = () => {
  const members = groupMembers.value || []
  const amount = parseFloat(model.value.amount) || 0

  // Initialize splits if empty or member count changed
  if (!model.value.splits || model.value.splits.length === 0 || model.value.splits.length !== members.length) {
    if (amount > 0 && members.length > 0) {
      const equalSplit = amount / members.length
      const roundedSplit = Math.floor(equalSplit * 100) / 100
      const remainder = amount - (roundedSplit * members.length)

      model.value.splits = members.map((m, index) => {
        return {
          userId: m.userId,
          included: true,
          splitAmount: index === 0 ? roundedSplit + remainder : roundedSplit,
        }
      })
    }
    else {
      model.value.splits = members.map((m) => {
        return {
          userId: m.userId,
          included: true,
          splitAmount: 0,
        }
      })
    }
  }
  else {
    // Smart adjustment: redistribute amount proportionally when it changes
    const currentTotal = splitTotal.value
    if (currentTotal > 0 && amount > 0 && Math.abs(currentTotal - amount) > 0.001) {
      const ratio = amount / currentTotal
      const includedSplits = model.value.splits.filter(s => s.included)

      if (includedSplits.length > 0) {
        let newTotal = 0
        includedSplits.forEach((split, index) => {
          if (index < includedSplits.length - 1) {
            split.splitAmount = Math.floor(split.splitAmount * ratio * 100) / 100
            newTotal += split.splitAmount
          }
        })
        // Assign remainder to last split to ensure exact total
        includedSplits[includedSplits.length - 1].splitAmount = amount - newTotal
      }
    }
  }
}

// Form submission
const onSubmit = async () => {
  const expenseData = {
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
  }

  emit('submit', {
    groupId: props.preSelectedGroupId || model.value.groupId,
    expenseData,
  })
}

const goBack = () => {
  emit('cancel')
}

// Initialize data on mount
onMounted(async () => {
  try {
    await fetchGroups()
  }
  catch (error) {
    console.error('Failed to load form data:', error)
  }
})
</script>
