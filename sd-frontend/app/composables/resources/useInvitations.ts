import type { Invitation, PendingUser, SendInvitationResponse, ValidateInvitationResponse, AcceptInvitationRequest } from '~/types/domain'

export default function useInvitations() {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const invitations = ref<Invitation[]>([])
  const pendingUsers = ref<PendingUser[]>([])
  const isLoading = ref(false)

  const sendInvitation = async (groupId: string, email: string) => {
    isLoading.value = true
    try {
      const response = await api.post<SendInvitationResponse>(`/groups/${groupId}/invitations`, { email })
      if (response.success && response.data) {
        if (response.data.type === 'member_added') {
          showSuccess(t('toasts.invitations.memberAdded'))
        }
        else {
          showSuccess(t('toasts.invitations.sent'))
        }
        return response.data
      }
    }
    catch (error: unknown) {
      const errData = error && typeof error === 'object' && 'data' in error
        ? (error as { data?: { error?: { message?: string } } }).data?.error?.message
        : undefined
      const message = errData || (t('toasts.invitations.sendFailed') as string) || ''
      showError(message)
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const fetchGroupInvitations = async (groupId: string) => {
    try {
      const response = await api.get<Invitation[]>(`/groups/${groupId}/invitations`)
      if (response.success && response.data) {
        invitations.value = response.data
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.invitations.loadFailed'))
      throw error
    }
  }

  const resendInvitation = async (groupId: string, invitationId: string) => {
    isLoading.value = true
    try {
      const response = await api.post<Invitation>(`/groups/${groupId}/invitations/${invitationId}/resend`)
      if (response.success && response.data) {
        const index = invitations.value.findIndex(i => i.id === invitationId)
        if (index !== -1) {
          invitations.value[index] = response.data
        }
        showSuccess(t('toasts.invitations.resent'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.invitations.resendFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const revokeInvitation = async (groupId: string, invitationId: string) => {
    isLoading.value = true
    try {
      await api.delete(`/groups/${groupId}/invitations/${invitationId}`)
      invitations.value = invitations.value.filter(i => i.id !== invitationId)
      showSuccess(t('toasts.invitations.revoked'))
    }
    catch (error: unknown) {
      showError(t('toasts.invitations.revokeFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const validateInvitationToken = async (token: string) => {
    isLoading.value = true
    try {
      const response = await api.get<ValidateInvitationResponse>('/invitations/validate', { token })
      if (response.success && response.data) {
        return response.data
      }
    }
    finally {
      isLoading.value = false
    }
  }

  const acceptInvitation = async (data: AcceptInvitationRequest) => {
    isLoading.value = true
    try {
      const response = await api.post<undefined>('/invitations/accept', data)
      if (response.success) {
        showSuccess(t('toasts.invitations.accountCreated'))
        return response
      }
    }
    catch (error: unknown) {
      const errData = error && typeof error === 'object' && 'data' in error
        ? (error as { data?: { error?: { message?: string } } }).data?.error?.message
        : undefined
      const message = errData || (t('toasts.invitations.accountCreateFailed') as string) || ''
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
      const response = await api.get<PendingUser[]>('/users/pending')
      if (response.success && response.data) {
        pendingUsers.value = response.data
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.invitations.pendingLoadFailed'))
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
