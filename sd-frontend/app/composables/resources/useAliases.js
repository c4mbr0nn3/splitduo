export default function useAliases() {
  const api = useApi()
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
      showError('Failed to load aliases')
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
        showSuccess('Alias created successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to create alias')
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
        showSuccess('Alias updated successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to update alias')
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
      showSuccess('Alias deleted successfully')
    }
    catch (error) {
      showError('Failed to delete alias')
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
        showSuccess('Member assigned successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to assign member')
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
      showSuccess('Member removed successfully')
    }
    catch (error) {
      showError('Failed to remove member')
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
        showSuccess('Alias setup finalized successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to finalize alias setup')
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
