import type { Alias, Group, CreateAliasRequest, UpdateAliasRequest, AssignAliasMemberRequest } from '~/types/domain'

export default function useAliases() {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const aliases = ref<Alias[]>([])
  const isLoading = ref(false)

  const fetchAliases = async (groupId: string) => {
    if (!groupId) return

    isLoading.value = true
    try {
      const response = await api.get<Alias[]>(`/groups/${groupId}/aliases`)
      if (response.success && response.data) {
        aliases.value = response.data
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.aliases.loadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const listAliases = async (groupId: string) => {
    return await fetchAliases(groupId)
  }

  const createAlias = async (groupId: string, { name }: CreateAliasRequest) => {
    isLoading.value = true
    try {
      const response = await api.post<Alias>(`/groups/${groupId}/aliases`, { name })
      if (response.success && response.data) {
        showSuccess(t('toasts.aliases.created'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.aliases.createFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const updateAlias = async (aliasId: string, { name }: UpdateAliasRequest) => {
    isLoading.value = true
    try {
      const response = await api.put<Alias>(`/aliases/${aliasId}`, { name })
      if (response.success && response.data) {
        showSuccess(t('toasts.aliases.updated'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.aliases.updateFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const deleteAlias = async (aliasId: string) => {
    isLoading.value = true
    try {
      await api.delete(`/aliases/${aliasId}`)
      showSuccess(t('toasts.aliases.deleted'))
    }
    catch (error: unknown) {
      showError(t('toasts.aliases.deleteFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const assignMember = async (aliasId: string, { userId }: AssignAliasMemberRequest) => {
    isLoading.value = true
    try {
      const response = await api.post<Alias>(`/aliases/${aliasId}/members`, { userId })
      if (response.success && response.data) {
        showSuccess(t('toasts.aliases.memberAssigned'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.aliases.memberAssignFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const removeMember = async (aliasId: string, userId: string) => {
    isLoading.value = true
    try {
      await api.delete(`/aliases/${aliasId}/members/${userId}`)
      showSuccess(t('toasts.aliases.memberRemoved'))
    }
    catch (error: unknown) {
      showError(t('toasts.aliases.memberRemoveFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const finalizeAliasSetup = async (groupId: string) => {
    isLoading.value = true
    try {
      const response = await api.post<Group>(`/groups/${groupId}/aliases/finalize`)
      if (response.success && response.data) {
        showSuccess(t('toasts.aliases.finalized'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.aliases.finalizeFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  return {
    aliases: readonly(aliases),
    isLoading: readonly(isLoading),
    fetchAliases,
    listAliases,
    createAlias,
    updateAlias,
    deleteAlias,
    assignMember,
    removeMember,
    finalizeAliasSetup,
  }
}
