import type {
  RecurringExpenseTemplate,
  RecurringExpenseInstance,
  Pagination,
  CreateRecurringExpenseTemplateRequest,
  UpdateRecurringExpenseTemplateRequest,
  ApproveRecurringExpenseInstanceRequest,
  ResumeStrategy,
  RecurrencePreviewRequest,
  RecurrencePreviewResponse,
  Expense,
} from '~/types/domain'

export default function useRecurringExpenses(groupId: string | Ref<string>) {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const templates = ref<RecurringExpenseTemplate[]>([])
  const currentTemplate = ref<RecurringExpenseTemplate | null>(null)
  const pendingInstances = ref<RecurringExpenseInstance[]>([])
  const pagination = ref<Pagination>({
    page: 1,
    limit: 20,
    total: 0,
    totalPages: 0,
    hasNext: false,
    hasPrev: false,
  })
  const isLoading = ref(false)

  // Fetch templates (optionally including inactive ones)
  const fetchTemplates = async (includeInactive = false) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get<RecurringExpenseTemplate[]>(
        `/groups/${groupIdRef.value}/recurring-expenses`,
        includeInactive ? { includeInactive: true } : undefined,
      )

      if (response.success && response.data) {
        templates.value = response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.templatesLoadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get single template
  const fetchTemplate = async (templateId: string) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get<RecurringExpenseTemplate>(`/groups/${groupIdRef.value}/recurring-expenses/${templateId}`)
      if (response.success && response.data) {
        currentTemplate.value = response.data
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.templateLoadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Create template
  const createTemplate = async (payload: CreateRecurringExpenseTemplateRequest) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post<RecurringExpenseTemplate>(
        `/groups/${groupIdRef.value}/recurring-expenses`,
        payload,
      )

      if (response.success && response.data) {
        templates.value.unshift(response.data)
        showSuccess(t('toasts.recurringExpenses.templateCreated'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.templateCreateFailed'))
      throw error
    }
  }

  // Update template
  const updateTemplate = async (templateId: string, payload: UpdateRecurringExpenseTemplateRequest) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.put<RecurringExpenseTemplate>(
        `/groups/${groupIdRef.value}/recurring-expenses/${templateId}`,
        payload,
      )

      if (response.success && response.data) {
        const index = templates.value.findIndex(tpl => tpl.id === templateId)
        if (index !== -1) {
          templates.value[index] = response.data
        }
        if (currentTemplate.value?.id === templateId) {
          currentTemplate.value = response.data
        }
        showSuccess(t('toasts.recurringExpenses.templateUpdated'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.templateUpdateFailed'))
      throw error
    }
  }

  // Toggle template active state
  const toggleTemplateActive = async (templateId: string, isActive: boolean) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post<RecurringExpenseTemplate>(
        `/groups/${groupIdRef.value}/recurring-expenses/${templateId}/toggle-active`,
        { isActive },
      )

      if (response.success && response.data) {
        const index = templates.value.findIndex(tpl => tpl.id === templateId)
        if (index !== -1) {
          templates.value[index] = response.data
        }
        if (currentTemplate.value?.id === templateId) {
          currentTemplate.value = response.data
        }
        showSuccess(t('toasts.recurringExpenses.templateUpdated'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.templateUpdateFailed'))
      throw error
    }
  }

  // Resume a paused template with an explicit strategy:
  // 'skip' permanently excludes missed periods, 'backfill' generates them.
  const resumeTemplate = async (templateId: string, strategy: ResumeStrategy) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post<RecurringExpenseTemplate>(
        `/groups/${groupIdRef.value}/recurring-expenses/${templateId}/resume`,
        { strategy },
      )

      if (response.success && response.data) {
        const index = templates.value.findIndex(tpl => tpl.id === templateId)
        if (index !== -1) {
          templates.value[index] = response.data
        }
        if (currentTemplate.value?.id === templateId) {
          currentTemplate.value = response.data
        }
        showSuccess(t('toasts.recurringExpenses.templateResumed'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.templateResumeFailed'))
      throw error
    }
  }

  // Delete template
  const deleteTemplate = async (templateId: string) => {
    if (!groupIdRef.value) return

    try {
      await api.delete(`/groups/${groupIdRef.value}/recurring-expenses/${templateId}`)
      templates.value = templates.value.filter(tpl => tpl.id !== templateId)
      if (currentTemplate.value?.id === templateId) {
        currentTemplate.value = null
      }
      showSuccess(t('toasts.recurringExpenses.templateDeleted'))
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.templateDeleteFailed'))
      throw error
    }
  }

  // Fetch instances (group-wide, or scoped to a single template)
  const fetchInstances = async (
    templateId?: string,
    status?: number | string,
    page = 1,
    limit = 20,
  ) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const params: Record<string, unknown> = {
        page,
        limit,
        ...(status !== undefined && { status }),
      }

      const response = templateId
        ? await api.getPaginated<RecurringExpenseInstance>(
            `/groups/${groupIdRef.value}/recurring-expenses/${templateId}/instances`,
            params,
          )
        : await api.getPaginated<RecurringExpenseInstance>(
            `/groups/${groupIdRef.value}/recurring-expenses/instances`,
            params,
          )

      if (response.success) {
        pendingInstances.value = response.data
        pagination.value = response.pagination || {
          page: 1, limit: 20, total: 0, totalPages: 0,
          hasNext: false, hasPrev: false,
        }
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.instancesLoadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Fetch instances pending approval (status 1 = PendingApproval)
  const fetchPendingInstances = async (page = 1, limit = 20) => {
    return fetchInstances(undefined, 'PendingApproval', page, limit)
  }

  // Approve an instance — backend creates the expense; optional payload overrides
  // amount/date/splits at approval time
  const approveInstance = async (instanceId: string, payload?: ApproveRecurringExpenseInstanceRequest) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post<Expense>(
        `/groups/${groupIdRef.value}/recurring-expenses/instances/${instanceId}/approve`,
        payload,
      )

      if (response.success && response.data) {
        pendingInstances.value = pendingInstances.value.filter(inst => inst.id !== instanceId)
        showSuccess(t('toasts.recurringExpenses.instanceApproved'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.instanceApproveFailed'))
      throw error
    }
  }

  // Reject an instance
  const rejectInstance = async (instanceId: string) => {
    if (!groupIdRef.value) return

    try {
      await api.post(`/groups/${groupIdRef.value}/recurring-expenses/instances/${instanceId}/reject`)
      pendingInstances.value = pendingInstances.value.filter(inst => inst.id !== instanceId)
      showSuccess(t('toasts.recurringExpenses.instanceRejected'))
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.instanceRejectFailed'))
      throw error
    }
  }

  // Preview upcoming occurrences for a recurrence rule (form helper — not persisted)
  const previewOccurrences = async (payload: RecurrencePreviewRequest) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.post<RecurrencePreviewResponse>(
        `/groups/${groupIdRef.value}/recurring-expenses/preview`,
        payload,
      )

      if (response.success && response.data) {
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.recurringExpenses.previewFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  return {
    templates: readonly(templates),
    currentTemplate: readonly(currentTemplate),
    pendingInstances: readonly(pendingInstances),
    pagination: readonly(pagination),
    isLoading: readonly(isLoading),
    fetchTemplates,
    fetchTemplate,
    createTemplate,
    updateTemplate,
    toggleTemplateActive,
    resumeTemplate,
    deleteTemplate,
    fetchInstances,
    fetchPendingInstances,
    approveInstance,
    rejectInstance,
    previewOccurrences,
  }
}
