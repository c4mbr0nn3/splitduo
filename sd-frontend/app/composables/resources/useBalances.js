export default function useBalances(groupId) {
  const api = useApi()
  const { showError } = useNotifications()
  const { fetchGroup, currentGroup } = useGroups()

  const groupIdRef = toRef(groupId)
  const balances = ref([])
  const balanceSummary = ref(null)
  const groupStats = ref(null)
  const isLoading = ref(false)

  const group = computed(() => currentGroup.value)
  const isAliasMode = computed(() => !!group.value?.useAliases)

  // Get current balances
  const fetchBalances = async () => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/balances`)
      if (response.success && response.data) {
        balances.value = normalizeBalances(response.data)
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
        balanceSummary.value = normalizeBalanceSummary(response.data)
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

  const fetchGroupStats = async () => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/stats`)
      if (response.success && response.data) {
        groupStats.value = normalizeGroupStats(response.data)
      }
    }
    catch (error) {
      showError('Failed to load group stats')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Normalize backend responses so consumers can branch with isAliasMode
  const normalizeBalances = (data) => {
    if (!Array.isArray(data)) return []
    if (!isAliasMode.value) return data
    return data.map(b => ({
      ...b,
      aliasId: b.aliasId,
      aliasName: b.aliasName,
      members: b.members || [],
      isSingleton: b.isSingleton,
    }))
  }

  const normalizeBalanceSummary = (data) => {
    if (!data) return null
    if (!isAliasMode.value) return data
    return {
      ...data,
      balances: data.balances || [],
      suggestions: data.suggestions || [],
    }
  }

  const normalizeGroupStats = (data) => {
    if (!data) return null
    if (!isAliasMode.value) return data
    return {
      ...data,
      balances: data.balances?.map(b => ({
        ...b,
        aliasId: b.aliasId,
        aliasName: b.aliasName,
        members: b.members || [],
        isSingleton: b.isSingleton,
      })) || [],
    }
  }

  return {
    balances: readonly(balances),
    balanceSummary: readonly(balanceSummary),
    groupStats: readonly(groupStats),
    isLoading: readonly(isLoading),
    isAliasMode: readonly(isAliasMode),
    group: readonly(group),
    fetchBalances,
    fetchBalanceSummary,
    fetchGroupStats,
    fetchGroup,
  }
}
