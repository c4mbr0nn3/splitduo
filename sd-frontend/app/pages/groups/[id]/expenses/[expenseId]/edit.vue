<template>
  <ExpensesExpenseForm
    v-model="expenseFormData"
    :title="$t('expenses.editExpense')"
    :submit-label="$t('expenses.updateExpense')"
    :pre-selected-group-id="groupId"
    :loading="isUpdating"
    @submit="onSubmit"
    @cancel="goBack"
  />
</template>

<script setup lang="ts">
import type { Expense } from '~/types/domain'

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

interface SplitItem {
  userId: string
  included: boolean
  splitAmount: number | null
}

interface AliasSplitItem {
  aliasId: string
  included: boolean
  splitAmount: number | null
}

const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)
const expenseId = route.params.expenseId ? String(route.params.expenseId) : undefined

const { currentExpense, fetchExpense, updateExpense } = useExpenses(groupId)

const isUpdating = ref(false)

// Form data state
const expenseFormData = ref<ExpenseFormModel>({
  groupId: undefined,
  title: '',
  description: '',
  amount: undefined,
  paidByUserId: undefined,
  expenseDate: new Date().toISOString().split('T')[0],
  categoryId: undefined,
  paymentModeId: undefined,
  splits: [],
})

// Watch currentExpense and update form data
watch(currentExpense, (expense) => {
  if (expense) {
    const e = expense as unknown as Expense
    const isAliasMode = Array.isArray(e.aliasSplits) && e.aliasSplits.length > 0
    expenseFormData.value = {
      expenseId: e.id,
      groupId: e.groupId,
      title: e.title,
      description: e.description ?? '',
      amount: String(e.amount ?? ''),
      paidByUserId: e.paidByUserId,
      expenseDate: e.expenseDate ? new Date(e.expenseDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0],
      categoryId: Number(e.categoryId) || undefined,
      paymentModeId: Number(e.paymentModeId) || undefined,
      splits: isAliasMode ? [] : (mapSplits(e.splits) || []),
      aliasSplits: isAliasMode ? (mapAliasSplits(e.aliasSplits) || []) : [],
    }
  }
}, { immediate: true })

const mapSplits = (splits: { userId?: string, splitAmount?: number | string }[] | undefined): SplitItem[] => {
  if (!Array.isArray(splits)) return []
  return splits.map((s) => {
    return {
      userId: s.userId ?? '',
      included: true,
      splitAmount: s.splitAmount != null ? Number(s.splitAmount) : null,
    }
  })
}

const mapAliasSplits = (aliasSplits: { aliasId?: string, splitAmount?: number | string }[] | null | undefined): AliasSplitItem[] => {
  if (!Array.isArray(aliasSplits)) return []
  return aliasSplits.map((s) => {
    return {
      aliasId: s.aliasId ?? '',
      included: true,
      splitAmount: s.splitAmount != null ? Number(s.splitAmount) : null,
    }
  })
}

const onSubmit = async (payload: { groupId: string, expenseData: Record<string, unknown> }) => {
  const { expenseData } = payload
  isUpdating.value = true
  try {
    const updatedExpense = await updateExpense(expenseId!, expenseData)
    if (updatedExpense) {
      await navigateTo(`/groups/${groupId}`)
    }
  }
  catch (error: unknown) {
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
  title: computed(() => `${t('expenses.editExpense')} - ${currentExpense.value?.title || ''}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
