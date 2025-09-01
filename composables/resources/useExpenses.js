export function useExpenses(groupId) {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const expenses = ref([])
  const currentExpense = ref(null)
  const pagination = ref({
    page: 1,
    limit: 20,
    total: 0,
    totalPages: 0,
    hasNext: false,
    hasPrev: false
  })
  const isLoading = ref(false)

  // Fetch expenses with filtering and pagination
  const fetchExpenses = async (filters = {}) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const params = {
        page: filters.page || 1,
        limit: filters.limit || 20,
        ...(filters.startDate && { startDate: filters.startDate }),
        ...(filters.endDate && { endDate: filters.endDate }),
        ...(filters.category && { category: filters.category }),
        ...(filters.userId && { userId: filters.userId })
      }

      const response = await api.get(
        `/groups/${groupIdRef.value}/expenses`,
        params
      )

      if (response.success && response.data) {
        expenses.value = response.data
        pagination.value = response.pagination || {
          page: 1, limit: 20, total: 0, totalPages: 0,
          hasNext: false, hasPrev: false
        }
      }
    } catch (error) {
      showError('Failed to load expenses')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Get single expense
  const fetchExpense = async (expenseId) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/expenses/${expenseId}`)
      if (response.success && response.data) {
        currentExpense.value = response.data
        return response.data
      }
    } catch (error) {
      showError('Failed to load expense')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Create expense
  const createExpense = async (expenseData) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post(
        `/groups/${groupIdRef.value}/expenses`,
        expenseData
      )

      if (response.success && response.data) {
        expenses.value.unshift(response.data)
        showSuccess('Expense created successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to create expense')
      throw error
    }
  }

  // Update expense
  const updateExpense = async (expenseId, updates) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.put(
        `/groups/${groupIdRef.value}/expenses/${expenseId}`,
        updates
      )

      if (response.success && response.data) {
        const index = expenses.value.findIndex(e => e.id === expenseId)
        if (index !== -1) {
          expenses.value[index] = response.data
        }
        if (currentExpense.value?.id === expenseId) {
          currentExpense.value = response.data
        }
        showSuccess('Expense updated successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to update expense')
      throw error
    }
  }

  // Delete expense
  const deleteExpense = async (expenseId) => {
    if (!groupIdRef.value) return

    try {
      await api.delete(`/groups/${groupIdRef.value}/expenses/${expenseId}`)
      expenses.value = expenses.value.filter(e => e.id !== expenseId)
      if (currentExpense.value?.id === expenseId) {
        currentExpense.value = null
      }
      showSuccess('Expense deleted successfully')
    } catch (error) {
      showError('Failed to delete expense')
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
    deleteExpense
  }
}