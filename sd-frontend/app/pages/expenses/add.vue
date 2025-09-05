<template>
  <div class="flex flex-col items-center justify-center p-4">
    <UCard class="w-full max-w-lg">
      <template #header>
        <h2 class="text-xl font-bold text-center">
          Add New Expense
        </h2>
      </template>
      <UForm
        :state="form"
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
            v-model="form.groupId"
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
            v-model="form.title"
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
            v-model="form.amount"
            :step="0.01"
            :min="0.01"
            :orientation="isMobile ? 'vertical' : 'horizontal'"
            placeholder="Enter the amount"
            size="lg"
            class="w-full"
          />
        </UFormField>
        <UFormField
          label="Description"
          name="description"
        >
          <UTextarea
            v-model="form.description"
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
            v-model="form.paidByUserId"
            :items="memberOptions"
            placeholder="Select who paid"
            size="lg"
            :disabled="!form.groupId"
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
            v-model="form.expenseDate"
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
            v-model="form.categoryId"
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
            v-model="form.paymentModeId"
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
              <UCard :variant="isMemberIncluded(member.userId) ? 'subtle' : 'outline'">
                <template #header>
                  <div class="flex items-center justify-between">
                    <span class="font-medium text-sm">
                      {{ member.user.firstName }} {{ member.user.lastName }}
                    </span>
                    <UCheckbox
                      :model-value="isMemberIncluded(member.userId)"
                      :color="isMemberIncluded(member.userId) ? 'success' : 'default'"
                      @update:model-value="toggleMember(member.userId)"
                    />
                  </div>
                </template>
                <UInputNumber
                  :model-value="getSplitAmount(member.userId)"
                  :step="0.001"
                  :min="0"
                  placeholder="Amount"
                  :variant="isMemberIncluded(member.userId) ? 'subtle' : 'ghost'"
                  :disabled="!isMemberIncluded(member.userId)"
                  class="w-full"
                  @update:model-value="updateSplitAmount(member.userId, $event)"
                />
              </UCard>
            </template>
          </div>
          <div class="text-xs text-muted">
            <span v-if="splitTotal > 0">
              Split total: {{ formatCurrency(splitTotal) }}
              <span
                v-if="form.amount && splitTotal !== parseFloat(form.amount)"
                class="text-orange-500"
              >
                ({{ splitTotal > parseFloat(form.amount) ? 'Over' : 'Under' }} by {{ formatCurrency(Math.abs(splitTotal - parseFloat(form.amount))) }})
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
            label="Add Expense"
            size="lg"
            class="flex-1"
            :loading="isCreating"
          />
        </div>
      </UForm>
    </UCard>
  </div>
</template>

<script setup>
const { isMobile } = useDevice()
const route = useRoute()
const { user } = useAuth()
const { groups, fetchGroups, fetchGroupMembers, isLoading: isLoadingGroups } = useGroups()
const { categories, fetchCategories, isLoading: isLoadingCategories } = useCategories()
const { paymentModes, fetchPaymentModes, isLoading: isLoadingPaymentModes } = usePaymentModes()

// Check if groupId is passed as query parameter (from group page) or route parameter
const preSelectedGroupId = computed(() => route.query.groupId || route.params.groupId)

// Form state
const form = ref({
  groupId: preSelectedGroupId.value || null,
  title: '',
  description: '',
  amount: '',
  paidByUserId: null,
  expenseDate: new Date().toISOString().split('T')[0], // Today's date
  categoryId: null,
  paymentModeId: null,
})

// Loading states
const isCreating = ref(false)
const isLoadingMembers = ref(false)

// Group members
const groupMembers = ref([])

// Splits state - array of { userId, splitAmount }
const splits = ref([])

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
  return splits.value.reduce((total, split) => total + parseFloat(split.splitAmount || 0), 0)
})

// Split helper functions
const isMemberIncluded = (userId) => {
  return splits.value.some(split => split.userId === userId)
}

const toggleMember = (userId) => {
  const index = splits.value.findIndex(split => split.userId === userId)
  if (index >= 0) {
    splits.value.splice(index, 1)
  }
  else {
    // Add member with equal split by default
    const equalAmount = form.value.amount ? (parseFloat(form.value.amount) / (splits.value.length + 1)).toFixed(4) : '0'
    splits.value.forEach((split) => {
      split.splitAmount = equalAmount
    })
    splits.value.push({
      userId: userId,
      splitAmount: equalAmount,
    })
  }
}

const getSplitAmount = (userId) => {
  const split = splits.value.find(split => split.userId === userId)
  return split ? split.splitAmount : ''
}

const updateSplitAmount = (userId, amount) => {
  const split = splits.value.find(split => split.userId === userId)
  if (split) {
    split.splitAmount = amount
  }
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

    // Reset splits when changing groups
    splits.value = []

    // Auto-select current user if they're in the group
    const currentUserMember = members?.find(m => m.userId === user.value?.id)
    if (currentUserMember) {
      form.value.paidByUserId = user.value.id
    }
    else if (members?.length > 0) {
      form.value.paidByUserId = members[0].userId
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
  () => form.value.groupId,
  (newGroupId) => {
    if (newGroupId) {
      loadGroupMembers(newGroupId)
    }
  },
  { immediate: true },
)

// Watch amount changes to recalculate equal splits
watch(
  () => form.value.amount,
  (newAmount) => {
    if (newAmount && splits.value.length > 0) {
      const equalAmount = (parseFloat(newAmount) / splits.value.length).toFixed(4)
      splits.value.forEach((split) => {
        split.splitAmount = equalAmount
      })
    }
  },
)

// Form validation
const validate = () => {
  const errors = []
  if (!form.value.groupId) {
    errors.push({ name: 'groupId', message: 'Group is required' })
  }
  if (!form.value.title) {
    errors.push({ name: 'title', message: 'Title is required' })
  }
  if (!form.value.amount) {
    errors.push({ name: 'amount', message: 'Amount is required' })
  }
  if (!form.value.paidByUserId) {
    errors.push({ name: 'paidByUserId', message: 'Paid By is required' })
  }
  if (!form.value.categoryId) {
    errors.push({ name: 'categoryId', message: 'Category is required' })
  }
  if (!form.value.paymentModeId) {
    errors.push({ name: 'paymentModeId', message: 'Payment Mode is required' })
  }
  return errors
}

// Form submission
const onSubmit = async () => {
  if (!form.value.groupId) {
    throw new Error('Group is required')
  }

  isCreating.value = true
  try {
    const { createExpense } = useExpenses(form.value.groupId)

    const expenseData = {
      title: form.value.title,
      description: form.value.description || null,
      amount: parseFloat(form.value.amount),
      paidByUserId: form.value.paidByUserId,
      expenseDate: form.value.expenseDate,
      categoryId: form.value.categoryId || undefined,
      paymentModeId: form.value.paymentModeId || undefined,
      splits: splits.value.length > 0
        ? splits.value.map(split => ({
            userId: split.userId,
            splitAmount: parseFloat(split.splitAmount),
          }))
        : undefined,
    }

    const createdExpense = await createExpense(expenseData)

    if (createdExpense) {
      // Redirect to group page
      await navigateTo(`/groups/${form.value.groupId}`)
    }
  }
  catch (error) {
    console.error('Failed to create expense:', error)
  }
  finally {
    isCreating.value = false
  }
}

const goBack = () => {
  if (preSelectedGroupId.value) {
    navigateTo(`/groups/${preSelectedGroupId.value}`)
  }
  else {
    navigateTo('/dashboard')
  }
}

// Initialize data
onMounted(async () => {
  try {
    // Load required data
    await Promise.all([
      fetchGroups(),
      fetchCategories(),
      fetchPaymentModes(),
    ])

    // Load group members if we have a pre-selected group
    if (preSelectedGroupId.value) {
      form.value.groupId = preSelectedGroupId.value
    }
  }
  catch (error) {
    console.error('Failed to load form data:', error)
  }
})

useHead({
  title: 'Add Expense',
})

definePageMeta({
  middleware: 'auth',
})
</script>
