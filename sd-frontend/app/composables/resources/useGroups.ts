import type { Group, GroupMember, CreateGroupRequest, UpdateGroupRequest, AddGroupMemberRequest } from '~/types/domain'

export default function useGroups() {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const groups = ref<Group[]>([])
  const currentGroup = ref<Group | null>(null)
  const isLoading = ref(false)

  // Get user's groups
  const fetchGroups = async (params: Record<string, unknown> = {}) => {
    isLoading.value = true
    try {
      const response = await api.get<Group[]>('/groups', params)
      if (response.success && response.data) {
        groups.value = response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.groups.loadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Create group
  const createGroup = async (groupData: CreateGroupRequest) => {
    isLoading.value = true
    try {
      const response = await api.post<Group>('/groups', groupData)
      if (response.success && response.data) {
        groups.value.push(response.data)
        showSuccess(t('toasts.groups.created'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.groups.createFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get group details
  const fetchGroup = async (groupId: string) => {
    isLoading.value = true
    try {
      const response = await api.get<Group>(`/groups/${groupId}`)
      if (response.success && response.data) {
        currentGroup.value = response.data
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.groups.detailsLoadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Update group
  const updateGroup = async (groupId: string, updates: UpdateGroupRequest) => {
    const updatePayload = {
      name: updates.name,
      description: updates.description,
    }

    isLoading.value = true
    try {
      const response = await api.put<Group>(`/groups/${groupId}`, updatePayload)
      if (response.success && response.data) {
        const index = groups.value.findIndex(g => g.id === groupId)
        if (index !== -1) {
          groups.value[index] = response.data
        }
        if (currentGroup.value?.id === groupId) {
          currentGroup.value = response.data
        }
        showSuccess(t('toasts.groups.updated'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.groups.updateFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Delete group
  const deleteGroup = async (groupId: string) => {
    isLoading.value = true
    try {
      await api.delete(`/groups/${groupId}`)
      groups.value = groups.value.filter(g => g.id !== groupId)
      if (currentGroup.value?.id === groupId) {
        currentGroup.value = null
      }
      showSuccess(t('toasts.groups.deleted'))
    }
    catch (error: unknown) {
      showError(t('toasts.groups.deleteFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get group members
  const fetchGroupMembers = async (groupId: string) => {
    try {
      const response = await api.get<GroupMember[]>(`/groups/${groupId}/members`)
      if (response.success && response.data) {
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.groups.membersLoadFailed'))
      throw error
    }
  }

  // Add group member
  const addGroupMember = async (groupId: string, memberData: AddGroupMemberRequest) => {
    try {
      const response = await api.post<GroupMember>(`/groups/${groupId}/members`, memberData)
      if (response.success && response.data) {
        showSuccess(t('toasts.groups.memberAdded'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.groups.memberAddFailed'))
      throw error
    }
  }

  // Remove group member
  const removeGroupMember = async (groupId: string, userId: string) => {
    try {
      await api.delete(`/groups/${groupId}/members/${userId}`)
      showSuccess(t('toasts.groups.memberRemoved'))
    }
    catch (error: unknown) {
      showError(t('toasts.groups.memberRemoveFailed'))
      throw error
    }
  }

  // Change member role
  const changeMemberRole = async (groupId: string, userId: string, role: string) => {
    try {
      const response = await api.put<GroupMember>(`/groups/${groupId}/members/${userId}/role`, { role })
      if (response.success && response.data) {
        showSuccess(t('toasts.groups.memberRoleUpdated'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.groups.memberRoleUpdateFailed'))
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
    changeMemberRole,
  }
}
