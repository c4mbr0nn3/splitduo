import type { Balance, AliasBalance, AliasBalanceSummary, BalanceSummaryUnion, GroupStats } from '~/types/domain'

export default function useBalances(groupId: string | Ref<string>) {
  const api = useApi()
  const { t } = useI18n()
  const { showError } = useNotifications()
  const { fetchGroup, currentGroup } = useGroups()

  const groupIdRef = toRef(groupId)
  const balances = ref<Balance[]>([])
  const balanceSummary = ref<BalanceSummaryUnion | null>(null)
  const groupStats = ref<GroupStats | null>(null)
  const isLoading = ref(false)

  const group = computed(() => currentGroup.value)
  const isAliasMode = computed(() => !!group.value?.useAliases)

  // Get current balances
  const fetchBalances = async () => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get<Balance[]>(`/groups/${groupIdRef.value}/balances`)
      if (response.success && response.data) {
        balances.value = normalizeBalances(response.data)
      }
    }
    catch (error: unknown) {
      showError(t('toasts.balances.loadFailed'))
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
      const response = await api.get<BalanceSummaryUnion>(`/groups/${groupIdRef.value}/balances/summary`)
      if (response.success && response.data) {
        balanceSummary.value = normalizeBalanceSummary(response.data)
      }
    }
    catch (error: unknown) {
      showError(t('toasts.balances.summaryLoadFailed'))
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
      const response = await api.get<GroupStats>(`/groups/${groupIdRef.value}/stats`)
      if (response.success && response.data) {
        groupStats.value = normalizeGroupStats(response.data)
      }
    }
    catch (error: unknown) {
      showError(t('toasts.balances.statsLoadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Normalize backend responses so consumers can branch with isAliasMode
  const normalizeBalances = (data: Balance[]): Balance[] => {
    if (!Array.isArray(data)) return []
    if (!isAliasMode.value) return data
    return data.map(b => ({
      ...b,
      aliasId: (b as AliasBalance).aliasId,
      aliasName: (b as AliasBalance).aliasName,
      members: (b as AliasBalance).members || [],
      isSingleton: (b as AliasBalance).isSingleton,
    }))
  }

  const normalizeBalanceSummary = (data: BalanceSummaryUnion): BalanceSummaryUnion | null => {
    if (!data) return null
    if (!isAliasMode.value) return data
    return {
      ...data,
      balances: (data as AliasBalanceSummary).balances || [],
      suggestions: (data as AliasBalanceSummary).suggestions || [],
    }
  }

  const normalizeGroupStats = (data: GroupStats): GroupStats | null => {
    if (!data) return null
    if (!isAliasMode.value) return data
    return {
      ...data,
      balances: data.balances?.map(b => ({
        ...b,
        aliasId: (b as AliasBalance).aliasId,
        aliasName: (b as AliasBalance).aliasName,
        members: (b as AliasBalance).members || [],
        isSingleton: (b as AliasBalance).isSingleton,
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
