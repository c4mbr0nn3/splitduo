export default function useGroups() {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groups = ref([])
  const currentGroup = ref(null)
  const isLoading = ref(false)

  // Get user's groups
  const fetchGroups = async (params = {}) => {
    isLoading.value = true
    try {
      const response = await api.get('/groups', params)
      if (response.success && response.data) {
        groups.value = response.data
      }
    }
    catch (error) {
      showError('Failed to load groups')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Create group
  const createGroup = async (groupData) => {
    isLoading.value = true
    try {
      const response = await api.post('/groups', groupData)
      if (response.success && response.data) {
        groups.value.push(response.data)
        showSuccess('Group created successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to create group')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get group details
  const fetchGroup = async (groupId) => {
    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupId}`)
      if (response.success && response.data) {
        currentGroup.value = response.data
        return response.data
      }
    }
    catch (error) {
      showError('Failed to load group details')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Update group
  const updateGroup = async (groupId, updates) => {
    const updatePayload = {
      name: updates.name,
      description: updates.description,
    }

    isLoading.value = true
    try {
      const response = await api.put(`/groups/${groupId}`, updatePayload)
      if (response.success && response.data) {
        const index = groups.value.findIndex(g => g.id === groupId)
        if (index !== -1) {
          groups.value[index] = response.data
        }
        if (currentGroup.value?.id === groupId) {
          currentGroup.value = response.data
        }
        showSuccess('Group updated successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to update group')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Delete group
  const deleteGroup = async (groupId) => {
    isLoading.value = true
    try {
      await api.delete(`/groups/${groupId}`)
      groups.value = groups.value.filter(g => g.id !== groupId)
      if (currentGroup.value?.id === groupId) {
        currentGroup.value = null
      }
      showSuccess('Group deleted successfully')
    }
    catch (error) {
      showError('Failed to delete group')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get group members
  const fetchGroupMembers = async (groupId) => {
    try {
      const response = await api.get(`/groups/${groupId}/members`)
      if (response.success && response.data) {
        return response.data
      }
    }
    catch (error) {
      showError('Failed to load group members')
      throw error
    }
  }

  // Add group member
  const addGroupMember = async (groupId, memberData) => {
    try {
      const response = await api.post(`/groups/${groupId}/members`, memberData)
      if (response.success && response.data) {
        showSuccess('Member added successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to add member')
      throw error
    }
  }

  // Remove group member
  const removeGroupMember = async (groupId, userId) => {
    try {
      await api.delete(`/groups/${groupId}/members/${userId}`)
      showSuccess('Member removed successfully')
    }
    catch (error) {
      showError('Failed to remove member')
      throw error
    }
  }

  return {
    groups: readonly(groups),
    currentGroup: readonly(currentGroup),
    isLoading: readonly(isLoading),
    fetchGroups,
    createGroup,
    fetchGroup,
    updateGroup,
    deleteGroup,
    fetchGroupMembers,
    addGroupMember,
    removeGroupMember,
  }
}
