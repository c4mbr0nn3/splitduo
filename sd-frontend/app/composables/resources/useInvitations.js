export default function useInvitations() {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const invitations = ref([])
  const pendingUsers = ref([])
  const isLoading = ref(false)

  const sendInvitation = async (groupId, email) => {
    isLoading.value = true
    try {
      const response = await api.post(`/groups/${groupId}/invitations`, { email })
      if (response.success && response.data) {
        if (response.data.type === 'member_added') {
          showSuccess('User added to group')
        }
        else {
          showSuccess('Invitation sent')
        }
        return response.data
      }
    }
    catch (error) {
      const message = error.data?.error?.message || 'Failed to send invitation'
      showError(message)
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const fetchGroupInvitations = async (groupId) => {
    try {
      const response = await api.get(`/groups/${groupId}/invitations`)
      if (response.success && response.data) {
        invitations.value = response.data
        return response.data
      }
    }
    catch (error) {
      showError('Failed to load invitations')
      throw error
    }
  }

  const resendInvitation = async (groupId, invitationId) => {
    isLoading.value = true
    try {
      const response = await api.post(`/groups/${groupId}/invitations/${invitationId}/resend`)
      if (response.success && response.data) {
        const index = invitations.value.findIndex(i => i.id === invitationId)
        if (index !== -1) {
          invitations.value[index] = response.data
        }
        showSuccess('Invitation resent')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to resend invitation')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const revokeInvitation = async (groupId, invitationId) => {
    isLoading.value = true
    try {
      await api.delete(`/groups/${groupId}/invitations/${invitationId}`)
      invitations.value = invitations.value.filter(i => i.id !== invitationId)
      showSuccess('Invitation revoked')
    }
    catch (error) {
      showError('Failed to revoke invitation')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const validateInvitationToken = async (token) => {
    isLoading.value = true
    try {
      const response = await api.get('/invitations/validate', { token })
      if (response.success && response.data) {
        return response.data
      }
    }
    finally {
      isLoading.value = false
    }
  }

  const acceptInvitation = async (data) => {
    isLoading.value = true
    try {
      const response = await api.post('/invitations/accept', data)
      if (response.success) {
        showSuccess('Account created successfully')
        return response
      }
    }
    catch (error) {
      const message = error.data?.error?.message || 'Failed to create account'
      showError(message)
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const fetchPendingInvitations = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/users/pending')
      if (response.success && response.data) {
        pendingUsers.value = response.data
        return response.data
      }
    }
    catch (error) {
      showError('Failed to load pending invitations')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  return {
    invitations: readonly(invitations),
    pendingUsers: readonly(pendingUsers),
    isLoading: readonly(isLoading),
    sendInvitation,
    fetchGroupInvitations,
    resendInvitation,
    revokeInvitation,
    validateInvitationToken,
    acceptInvitation,
    fetchPendingInvitations,
  }
}
