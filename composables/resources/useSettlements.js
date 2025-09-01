export function useSettlements(groupId) {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const settlements = ref([])
  const currentSettlement = ref(null)
  const pagination = ref({
    page: 1,
    limit: 20,
    total: 0,
    totalPages: 0,
    hasNext: false,
    hasPrev: false
  })
  const isLoading = ref(false)

  // Fetch settlements with filtering and pagination
  const fetchSettlements = async (filters = {}) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const params = {
        page: filters.page || 1,
        limit: filters.limit || 20,
        ...(filters.startDate && { startDate: filters.startDate }),
        ...(filters.endDate && { endDate: filters.endDate })
      }

      const response = await api.get(
        `/groups/${groupIdRef.value}/settlements`,
        params
      )

      if (response.success && response.data) {
        settlements.value = response.data
        pagination.value = response.pagination || {
          page: 1, limit: 20, total: 0, totalPages: 0,
          hasNext: false, hasPrev: false
        }
      }
    } catch (error) {
      showError('Failed to load settlements')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Create settlement
  const createSettlement = async (settlementData) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post(
        `/groups/${groupIdRef.value}/settlements`,
        settlementData
      )

      if (response.success && response.data) {
        settlements.value.unshift(response.data)
        showSuccess('Settlement created successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to create settlement')
      throw error
    }
  }

  // Update settlement
  const updateSettlement = async (settlementId, updates) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.put(
        `/groups/${groupIdRef.value}/settlements/${settlementId}`,
        updates
      )

      if (response.success && response.data) {
        const index = settlements.value.findIndex(s => s.id === settlementId)
        if (index !== -1) {
          settlements.value[index] = response.data
        }
        if (currentSettlement.value?.id === settlementId) {
          currentSettlement.value = response.data
        }
        showSuccess('Settlement updated successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to update settlement')
      throw error
    }
  }

  // Delete settlement
  const deleteSettlement = async (settlementId) => {
    if (!groupIdRef.value) return

    try {
      await api.delete(`/groups/${groupIdRef.value}/settlements/${settlementId}`)
      settlements.value = settlements.value.filter(s => s.id !== settlementId)
      if (currentSettlement.value?.id === settlementId) {
        currentSettlement.value = null
      }
      showSuccess('Settlement deleted successfully')
    } catch (error) {
      showError('Failed to delete settlement')
      throw error
    }
  }

  // Confirm settlement
  const confirmSettlement = async (settlementId) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post(
        `/groups/${groupIdRef.value}/settlements/${settlementId}/confirm`
      )

      if (response.success && response.data) {
        const index = settlements.value.findIndex(s => s.id === settlementId)
        if (index !== -1) {
          settlements.value[index] = response.data
        }
        if (currentSettlement.value?.id === settlementId) {
          currentSettlement.value = response.data
        }
        showSuccess('Settlement confirmed successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to confirm settlement')
      throw error
    }
  }

  return {
    settlements: readonly(settlements),
    currentSettlement: readonly(currentSettlement),
    pagination: readonly(pagination),
    isLoading: readonly(isLoading),
    fetchSettlements,
    createSettlement,
    updateSettlement,
    deleteSettlement,
    confirmSettlement
  }
}