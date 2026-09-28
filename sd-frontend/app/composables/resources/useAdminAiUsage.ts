import type {
  AiAdminConfig,
  AiUsageEntry,
  AiUsageSummary,
  Pagination,
} from '~/types/domain'

export interface AiUsageFilters {
  page?: number
  limit?: number
  from?: number
  to?: number
  userId?: string
  success?: boolean
}

export default function useAdminAiUsage() {
  const api = useApi()
  const { t } = useI18n()
  const { showError } = useNotifications()

  const entries = ref<AiUsageEntry[]>([])
  const summary = ref<AiUsageSummary | null>(null)
  const config = ref<AiAdminConfig | null>(null)
  const pagination = ref<Pagination>({
    page: 1, limit: 20, total: 0, totalPages: 0, hasNext: false, hasPrev: false,
  })
  const isLoadingList = ref(false)
  const isLoadingSummary = ref(false)
  const isLoadingConfig = ref(false)

  const buildParams = (filters: AiUsageFilters, includePaging: boolean) => {
    const params: Record<string, unknown> = {}
    if (includePaging) {
      params.page = filters.page ?? 1
      params.limit = filters.limit ?? 20
    }
    // NOTE: use != null (not truthiness) — `success: false` must NOT be dropped.
    if (filters.from != null) params.from = filters.from
    if (filters.to != null) params.to = filters.to
    if (filters.userId) params.userId = filters.userId
    if (filters.success != null) params.success = filters.success
    return params
  }

  const fetchUsage = async (filters: AiUsageFilters = {}) => {
    isLoadingList.value = true
    try {
      const response = await api.getPaginated<AiUsageEntry>('/admin/ai/usage', buildParams(filters, true))
      if (response.success) {
        entries.value = response.data
        pagination.value = response.pagination
      }
    }
    catch {
      showError(t('toasts.adminAiUsage.loadFailed'))
    }
    finally {
      isLoadingList.value = false
    }
  }

  const fetchSummary = async (filters: AiUsageFilters = {}) => {
    isLoadingSummary.value = true
    try {
      const response = await api.get<AiUsageSummary>('/admin/ai/usage/summary', buildParams(filters, false))
      if (response.success && response.data) summary.value = response.data
    }
    catch {
      showError(t('toasts.adminAiUsage.summaryLoadFailed'))
    }
    finally {
      isLoadingSummary.value = false
    }
  }

  const fetchConfig = async () => {
    isLoadingConfig.value = true
    try {
      const response = await api.get<AiAdminConfig>('/admin/ai/config')
      if (response.success && response.data) config.value = response.data
    }
    catch {
      showError(t('toasts.adminAiUsage.configLoadFailed'))
    }
    finally {
      isLoadingConfig.value = false
    }
  }

  return {
    entries: readonly(entries),
    summary: readonly(summary),
    config: readonly(config),
    pagination: readonly(pagination),
    isLoadingList: readonly(isLoadingList),
    isLoadingSummary: readonly(isLoadingSummary),
    isLoadingConfig: readonly(isLoadingConfig),
    fetchUsage,
    fetchSummary,
    fetchConfig,
  }
}
