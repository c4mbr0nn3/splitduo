export default function useAliases() {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const aliases = ref([])
  const isLoading = ref(false)

  const fetchAliases = async (groupId) => {
    if (!groupId) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupId}/aliases`)
      if (response.success && response.data) {
        aliases.value = response.data
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.aliases.loadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const listAliases = async (groupId) => {
    return await fetchAliases(groupId)
  }

  const createAlias = async (groupId, { name }) => {
    isLoading.value = true
    try {
      const response = await api.post(`/groups/${groupId}/aliases`, { name })
      if (response.success && response.data) {
        showSuccess(t('toasts.aliases.created'))
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.aliases.createFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const updateAlias = async (aliasId, { name }) => {
    isLoading.value = true
    try {
      const response = await api.put(`/aliases/${aliasId}`, { name })
      if (response.success && response.data) {
        showSuccess(t('toasts.aliases.updated'))
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.aliases.updateFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const deleteAlias = async (aliasId) => {
    isLoading.value = true
    try {
      await api.delete(`/aliases/${aliasId}`)
      showSuccess(t('toasts.aliases.deleted'))
    }
    catch (error) {
      showError(t('toasts.aliases.deleteFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const assignMember = async (aliasId, { userId }) => {
    isLoading.value = true
    try {
      const response = await api.post(`/aliases/${aliasId}/members`, { userId })
      if (response.success && response.data) {
        showSuccess(t('toasts.aliases.memberAssigned'))
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.aliases.memberAssignFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const removeMember = async (aliasId, userId) => {
    isLoading.value = true
    try {
      await api.delete(`/aliases/${aliasId}/members/${userId}`)
      showSuccess(t('toasts.aliases.memberRemoved'))
    }
    catch (error) {
      showError(t('toasts.aliases.memberRemoveFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const finalizeAliasSetup = async (groupId) => {
    isLoading.value = true
    try {
      const response = await api.post(`/groups/${groupId}/aliases/finalize`)
      if (response.success && response.data) {
        showSuccess(t('toasts.aliases.finalized'))
        return response.data
      }
    }
    catch (error) {
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
