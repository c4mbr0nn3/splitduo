import type { Settlement, Pagination, CreateSettlementRequest } from '~/types/domain'

export interface SettlementFilters {
  page?: number
  limit?: number
}

export default function useSettlements(groupId: string | Ref<string>) {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const settlements = ref<Settlement[]>([])
  const pagination = ref<Pagination>({
    page: 1,
    limit: 10,
    total: 0,
    totalPages: 0,
    hasNext: false,
    hasPrev: false,
  })
  const isLoading = ref(false)

  const fetchSettlements = async (filters: SettlementFilters = {}) => {
    if (!groupIdRef.value) return
    isLoading.value = true
    try {
      const response = await api.getPaginated<Settlement>(`/groups/${groupIdRef.value}/settlements`, {
        page: filters.page || 1,
        limit: filters.limit || 10,
      })
      if (response.success) {
        settlements.value = response.data
        pagination.value = response.pagination || {
          page: 1, limit: 10, total: 0, totalPages: 0, hasNext: false, hasPrev: false,
        }
      }
    }
    catch (error: unknown) {
      showError(t('toasts.settlements.loadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const createSettlement = async (payload: CreateSettlementRequest) => {
    if (!groupIdRef.value) return
    try {
      const response = await api.post<Settlement>(`/groups/${groupIdRef.value}/settlements`, payload)
      if (response.success && response.data) {
        settlements.value.unshift(response.data)
        showSuccess(t('toasts.settlements.created'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.settlements.createFailed'))
      throw error
    }
  }

  const deleteSettlement = async (settlementId: string) => {
    if (!groupIdRef.value) return
    try {
      await api.delete(`/groups/${groupIdRef.value}/settlements/${settlementId}`)
      settlements.value = settlements.value.filter(s => s.id !== settlementId)
      showSuccess(t('toasts.settlements.deleted'))
    }
    catch (error: unknown) {
      showError(t('toasts.settlements.deleteFailed'))
      throw error
    }
  }

  return {
    settlements: readonly(settlements),
    pagination: readonly(pagination),
    isLoading: readonly(isLoading),
    fetchSettlements,
    createSettlement,
    deleteSettlement,
  }
}
