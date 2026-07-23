<template>
  <ExpensesExpenseForm
    v-model="expenseFormData"
    title="Edit Expense"
    submit-label="Update Expense"
    :pre-selected-group-id="groupId"
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

// Form data state
const expenseFormData = ref({
  groupId: null,
  title: '',
  description: '',
  amount: null,
  paidByUserId: null,
  expenseDate: new Date().toISOString().split('T')[0],
  categoryId: null,
  paymentModeId: null,
  splits: [],
})

// Watch currentExpense and update form data
watch(currentExpense, (expense) => {
  if (expense) {
    const isAliasMode = Array.isArray(expense.aliasSplits) && expense.aliasSplits.length > 0
    expenseFormData.value = {
      expenseId: expense.id,
      groupId: expense.groupId,
      title: expense.title,
      description: expense.description,
      amount: expense.amount,
      paidByUserId: expense.paidByUserId,
      expenseDate: expense.expenseDate ? new Date(expense.expenseDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0],
      categoryId: expense.categoryId,
      paymentModeId: expense.paymentModeId,
      splits: isAliasMode ? [] : (mapSplits(expense.splits) || []),
      aliasSplits: isAliasMode ? (mapAliasSplits(expense.aliasSplits) || []) : [],
    }
  }
}, { immediate: true })

const mapSplits = (splits) => {
  if (!Array.isArray(splits)) return []
  return splits.map((s) => {
    return {
      userId: s.userId,
      included: true,
      splitAmount: s.splitAmount,
    }
  })
}

const mapAliasSplits = (aliasSplits) => {
  if (!Array.isArray(aliasSplits)) return []
  return aliasSplits.map((s) => {
    return {
      aliasId: s.aliasId,
      included: true,
      splitAmount: s.splitAmount,
    }
  })
}

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

const { goBack } = useSmartBack(`/groups/${groupId}`)

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
