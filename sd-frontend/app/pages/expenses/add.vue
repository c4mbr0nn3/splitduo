<template>
  <ExpensesExpenseForm
    v-model="expenseFormData"
    :title="$t('expenses.addNew')"
    :submit-label="$t('expenses.addExpense')"
    :pre-selected-group-id="preSelectedGroupId"
    :loading="isCreating"
    show-add-more
    @submit="onSubmit"
    @add-more="onAddMore"
    @cancel="goBack"
  />
</template>

<script setup lang="ts">
import type { CreateExpenseRequest } from '~/types/domain'

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
  splits?: { userId: string, included: boolean, splitAmount: number | null }[]
  aliasSplits?: { aliasId: string, included: boolean, splitAmount: number | null }[]
}

const { t } = useI18n()
const route = useRoute()
const { clearReceiptImage } = useReceiptScan()

onUnmounted(() => clearReceiptImage())

// Check if groupId is passed as query parameter (from group page) or route parameter
const preSelectedGroupId = computed<string | null>(() => {
  const id = route.query.groupId || route.params.groupId
  return id ? String(id) : null
})

const getInitialFormData = (): ExpenseFormModel => ({
  groupId: preSelectedGroupId.value ?? undefined,
  title: typeof route.query.title === 'string' ? route.query.title : '',
  description: typeof route.query.description === 'string' ? route.query.description : '',
  amount: route.query.amount ? String(route.query.amount) : undefined,
  paidByUserId: undefined,
  expenseDate: typeof route.query.expenseDate === 'string' ? route.query.expenseDate : new Date().toISOString().split('T')[0],
  categoryId: route.query.categoryId ? Number(route.query.categoryId) : undefined,
  paymentModeId: route.query.paymentModeId ? Number(route.query.paymentModeId) : undefined,
  splits: [],
})

// Form data state
const expenseFormData = ref(getInitialFormData())

// Loading states
const isCreating = ref(false)

// useExpenses must be called at setup scope (it uses useI18n/useNotifications).
// groupId isn't known until submit, so bridge via a reactive ref.
const activeGroupId = ref('')
const { createExpense } = useExpenses(activeGroupId)

// Form submission
const onSubmit = async (payload: { groupId: string, expenseData: Record<string, unknown> }) => {
  const { groupId, expenseData } = payload
  activeGroupId.value = groupId
  isCreating.value = true
  try {
    const createdExpense = await createExpense(expenseData as CreateExpenseRequest)

    if (createdExpense) {
      clearReceiptImage()
      await navigateTo(`/groups/${groupId}`)
    }
  }
  catch (error: unknown) {
    console.error('Failed to create expense:', error)
  }
  finally {
    isCreating.value = false
  }
}

const onAddMore = async (payload: { groupId: string, expenseData: Record<string, unknown> }) => {
  const { groupId, expenseData } = payload
  activeGroupId.value = groupId
  isCreating.value = true
  try {
    const createdExpense = await createExpense(expenseData as CreateExpenseRequest)

    if (createdExpense) {
      clearReceiptImage()
      const resetData = getInitialFormData()
      resetData.groupId = groupId
      expenseFormData.value = resetData
    }
  }
  catch (error: unknown) {
    console.error('Failed to create expense:', error)
  }
  finally {
    isCreating.value = false
  }
}

const { goBack } = useSmartBack(`/groups/${route.query.groupId}`)

useHead({
  title: computed(() => t('expenses.addNew')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
