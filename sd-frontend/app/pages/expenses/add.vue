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
const router = useRouter()
const { clearReceiptImage } = useReceiptScan()

onUnmounted(() => clearReceiptImage())

// Check if groupId is passed as query parameter (from group page) or route parameter
const preSelectedGroupId = computed(() => route.query.groupId || route.params.groupId)

const getInitialFormData = () => ({
  groupId: route.query.groupId || preSelectedGroupId.value || null,
  title: route.query.title || '',
  description: route.query.description || '',
  amount: route.query.amount ? Number(route.query.amount) : null,
  paidByUserId: null,
  expenseDate: route.query.expenseDate || new Date().toISOString().split('T')[0],
  categoryId: route.query.categoryId ? Number(route.query.categoryId) : null,
  paymentModeId: route.query.paymentModeId ? Number(route.query.paymentModeId) : null,
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
      clearReceiptImage()
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
      clearReceiptImage()
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
  router.back()
}

useHead({
  title: 'Add Expense',
})

definePageMeta({
  middleware: 'auth',
})
</script>
