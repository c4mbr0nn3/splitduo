export default function useBalances(groupId) {
  const api = useApi()
  const { showError } = useNotifications()

  const groupIdRef = toRef(groupId)
  const balances = ref([])
  const balanceSummary = ref(null)
  const isLoading = ref(false)

  // Get current balances
  const fetchBalances = async () => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/balances`)
      if (response.success && response.data) {
        balances.value = response.data
      }
    }
    catch (error) {
      showError('Failed to load balances')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get balance summary with suggestions
  const fetchBalanceSummary = async () => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/balances/summary`)
      if (response.success && response.data) {
        balanceSummary.value = response.data
      }
    }
    catch (error) {
      showError('Failed to load balance summary')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  return {
    balances: readonly(balances),
    balanceSummary: readonly(balanceSummary),
    isLoading: readonly(isLoading),
    fetchBalances,
    fetchBalanceSummary,
  }
}
