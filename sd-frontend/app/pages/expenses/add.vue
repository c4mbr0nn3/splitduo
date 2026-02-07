<template>
  <ExpensesExpenseForm
    v-model="expenseFormData"
    title="Add New Expense"
    submit-label="Add Expense"
    :pre-selected-group-id="preSelectedGroupId"
    :loading="isCreating"
    show-add-more
    @submit="onSubmit"
    @add-more="onAddMore"
    @cancel="goBack"
  />
</template>

<script setup>
const route = useRoute()

// Check if groupId is passed as query parameter (from group page) or route parameter
const preSelectedGroupId = computed(() => route.query.groupId || route.params.groupId)

const getInitialFormData = () => ({
  groupId: preSelectedGroupId.value || null,
  title: '',
  description: '',
  amount: null,
  paidByUserId: null,
  expenseDate: new Date().toISOString().split('T')[0],
  categoryId: null,
  paymentModeId: null,
  splits: [],
})

// Form data state
const expenseFormData = ref(getInitialFormData())

// Loading states
const isCreating = ref(false)

// Form submission
const onSubmit = async ({ groupId, expenseData }) => {
  isCreating.value = true
  try {
    const { createExpense } = useExpenses(groupId)
    const createdExpense = await createExpense(expenseData)

    if (createdExpense) {
      await navigateTo(`/groups/${groupId}`)
    }
  }
  catch (error) {
    console.error('Failed to create expense:', error)
  }
  finally {
    isCreating.value = false
  }
}

const onAddMore = async ({ groupId, expenseData }) => {
  isCreating.value = true
  try {
    const { createExpense } = useExpenses(groupId)
    const createdExpense = await createExpense(expenseData)

    if (createdExpense) {
      const resetData = getInitialFormData()
      resetData.groupId = groupId
      expenseFormData.value = resetData
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

useHead({
  title: 'Add Expense',
})

definePageMeta({
  middleware: 'auth',
})
</script>
