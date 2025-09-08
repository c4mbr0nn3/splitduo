<template>
  <ExpensesExpenseForm
    title="Edit Expense"
    submit-label="Update Expense"
    :pre-selected-group-id="groupId"
    :initial-data="initialData"
    :loading="isUpdating"
    @submit="onSubmit"
    @cancel="goBack"
  />
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id
const expenseId = route.params.expenseId

const { currentExpense, fetchExpense, updateExpense } = useExpenses(groupId)

const isUpdating = ref(false)

const initialData = computed(() => {
  if (!currentExpense.value) return null

  return {
    groupId: currentExpense.value.groupId,
    title: currentExpense.value.title,
    description: currentExpense.value.description,
    amount: currentExpense.value.amount,
    paidByUserId: currentExpense.value.paidByUserId,
    expenseDate: currentExpense.value.expenseDate ? new Date(currentExpense.value.expenseDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0],
    categoryId: currentExpense.value.categoryId,
    paymentModeId: currentExpense.value.paymentModeId,
    splits: currentExpense.value.splits || [],
  }
})

const onSubmit = async ({ expenseData }) => {
  isUpdating.value = true
  try {
    const updatedExpense = await updateExpense(expenseId, expenseData)
    if (updatedExpense) {
      await navigateTo(`/groups/${groupId}`)
    }
  }
  catch (error) {
    console.error('Failed to update expense:', error)
  }
  finally {
    isUpdating.value = false
  }
}

const goBack = () => {
  navigateTo(`/groups/${groupId}`)
}

onMounted(async () => {
  if (groupId && expenseId) {
    await fetchExpense(expenseId)
  }
})

useHead({
  title: computed(() => `Edit ${currentExpense.value?.title || 'Expense'}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
