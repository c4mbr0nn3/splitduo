import type { Expense, Pagination, CreateExpenseRequest, UpdateExpenseRequest } from '~/types/domain'

export interface ExpenseFilters {
  page?: number
  limit?: number
  startDate?: string
  endDate?: string
  category?: string
  userId?: string
  search?: string
}

export default function useExpenses(groupId: string | Ref<string>) {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const expenses = ref<Expense[]>([])
  const currentExpense = ref<Expense | null>(null)
  const pagination = ref<Pagination>({
    page: 1,
    limit: 20,
    total: 0,
    totalPages: 0,
    hasNext: false,
    hasPrev: false,
  })
  const isLoading = ref(false)

  // Fetch expenses with filtering and pagination
  const fetchExpenses = async (filters: ExpenseFilters = {}) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const params: Record<string, unknown> = {
        page: filters.page || 1,
        limit: filters.limit || 20,
        ...(filters.startDate && { startDate: filters.startDate }),
        ...(filters.endDate && { endDate: filters.endDate }),
        ...(filters.category && { category: filters.category }),
        ...(filters.userId && { userId: filters.userId }),
        ...(filters.search && { search: filters.search }),
      }

      const response = await api.getPaginated<Expense>(
        `/groups/${groupIdRef.value}/expenses`,
        params,
      )

      if (response.success) {
        expenses.value = response.data
        pagination.value = response.pagination || {
          page: 1, limit: 20, total: 0, totalPages: 0,
          hasNext: false, hasPrev: false,
        }
      }
    }
    catch (error: unknown) {
      showError(t('toasts.expenses.loadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get single expense
  const fetchExpense = async (expenseId: string) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get<Expense>(`/groups/${groupIdRef.value}/expenses/${expenseId}`)
      if (response.success && response.data) {
        currentExpense.value = response.data
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.expenses.loadOneFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Create expense
  const createExpense = async (expenseData: CreateExpenseRequest) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post<Expense>(
        `/groups/${groupIdRef.value}/expenses`,
        expenseData,
      )

      if (response.success && response.data) {
        expenses.value.unshift(response.data)
        showSuccess(t('toasts.expenses.created'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.expenses.createFailed'))
      throw error
    }
  }

  // Update expense
  const updateExpense = async (expenseId: string, updates: UpdateExpenseRequest) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.put<Expense>(
        `/groups/${groupIdRef.value}/expenses/${expenseId}`,
        updates,
      )

      if (response.success && response.data) {
        const index = expenses.value.findIndex(e => e.id === expenseId)
        if (index !== -1) {
          expenses.value[index] = response.data
        }
        if (currentExpense.value?.id === expenseId) {
          currentExpense.value = response.data
        }
        showSuccess(t('toasts.expenses.updated'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.expenses.updateFailed'))
      throw error
    }
  }

  // Delete expense
  const deleteExpense = async (expenseId: string) => {
    if (!groupIdRef.value) return

    try {
      await api.delete(`/groups/${groupIdRef.value}/expenses/${expenseId}`)
      expenses.value = expenses.value.filter(e => e.id !== expenseId)
      if (currentExpense.value?.id === expenseId) {
        currentExpense.value = null
      }
      showSuccess(t('toasts.expenses.deleted'))
    }
    catch (error: unknown) {
      showError(t('toasts.expenses.deleteFailed'))
      throw error
    }
  }

  return {
    expenses: readonly(expenses),
    currentExpense: readonly(currentExpense),
    pagination: readonly(pagination),
    isLoading: readonly(isLoading),
    fetchExpenses,
    fetchExpense,
    createExpense,
    updateExpense,
    deleteExpense,
  }
}
