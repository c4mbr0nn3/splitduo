import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import useInvitations from './useInvitations'
import { apiMock } from '~/composables/api/base.mock'
import type { Invitation, PendingUser, SendInvitationResponse, ValidateInvitationResponse } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useInvitations.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const invitation = (overrides: Partial<Invitation> = {}): Invitation => ({
  id: 'inv-1',
  email: 'alice@example.com',
  invitedBy: { id: 'user-1', email: 'bob@example.com', firstName: 'Bob' },
  groupName: 'Family',
  invitedAt: 1700000000000,
  expiresAt: 1700600000000,
  ...overrides,
})

const pendingUser = (overrides: Partial<PendingUser> = {}): PendingUser => ({
  email: 'alice@example.com',
  groups: [{ id: 'group-1', name: 'Family', invitedAt: 1700000000000, expiresAt: 1700600000000 }],
  ...overrides,
})

const sendInvitationResponse = (overrides: Partial<SendInvitationResponse> = {}): SendInvitationResponse => ({
  type: 'invitation_sent',
  ...overrides,
})

const validateResponse = (overrides: Partial<ValidateInvitationResponse> = {}): ValidateInvitationResponse => ({
  email: 'alice@example.com',
  groupName: 'Family',
  expiresAt: 1700600000000,
  ...overrides,
})

/** Error shape the API layer surfaces for a structured backend error. */
const structuredError = (message: string) => ({ data: { error: { message } } })

const acceptRequest = {
  token: 'token-123',
  firstName: 'Alice',
  lastName: 'Smith',
  password: 'secret123',
  confirmPassword: 'secret123',
}

describe('useInvitations', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('sendInvitation', () => {
    it('shows the member-added toast and returns the response data when the user is already a member', async () => {
      const data = sendInvitationResponse({ type: 'member_added' })
      apiMock.post.mockResolvedValue({ success: true, data })
      const inv = useInvitations()

      const result = await inv.sendInvitation('group-1', 'alice@example.com')

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/invitations', { email: 'alice@example.com' })
      expect(tMock).toHaveBeenCalledWith('toasts.invitations.memberAdded')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.invitations.memberAdded')
      expect(result).toEqual(data)
    })

    it('shows the sent toast and returns the response data for a new invitation', async () => {
      const data = sendInvitationResponse({ type: 'invitation_sent' })
      apiMock.post.mockResolvedValue({ success: true, data })
      const inv = useInvitations()

      const result = await inv.sendInvitation('group-1', 'alice@example.com')

      expect(tMock).toHaveBeenCalledWith('toasts.invitations.sent')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.invitations.sent')
      expect(result).toEqual(data)
    })

    it('shows the extracted backend message and re-throws when the API returns a structured error', async () => {
      const error = structuredError('Email already in group')
      apiMock.post.mockRejectedValue(error)
      const inv = useInvitations()

      await expect(inv.sendInvitation('group-1', 'alice@example.com')).rejects.toBe(error)

      expect(notificationsMock.showError).toHaveBeenCalledWith('Email already in group')
      expect(tMock).not.toHaveBeenCalledWith('toasts.invitations.sendFailed')
      expect(inv.isLoading.value).toBe(false)
    })

    it('shows the fallback toast and re-throws when the API fails without a structured error', async () => {
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const inv = useInvitations()

      await expect(inv.sendInvitation('group-1', 'alice@example.com')).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.invitations.sendFailed')
      expect(inv.isLoading.value).toBe(false)
    })
  })

  describe('fetchGroupInvitations', () => {
    it('stores the invitations and returns them', async () => {
      const invitations = [invitation({ id: 'inv-1' }), invitation({ id: 'inv-2', email: 'carol@example.com' })]
      apiMock.get.mockResolvedValue({ success: true, data: invitations })
      const inv = useInvitations()

      const result = await inv.fetchGroupInvitations('group-1')

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/invitations')
      expect(inv.invitations.value).toEqual(invitations)
      expect(result).toEqual(invitations)
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Load failed'))
      const inv = useInvitations()

      await expect(inv.fetchGroupInvitations('group-1')).rejects.toThrow('Load failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.invitations.loadFailed')
    })
  })

  describe('resendInvitation', () => {
    it('replaces the matching invitation in the list, shows a success toast, and returns the updated invitation', async () => {
      const original = invitation({ id: 'inv-1', email: 'alice@example.com' })
      const updated = invitation({ id: 'inv-1', email: 'alice@example.com', groupName: 'Family (updated)' })
      apiMock.get.mockResolvedValue({ success: true, data: [original] })
      apiMock.post.mockResolvedValue({ success: true, data: updated })
      const inv = useInvitations()
      await inv.fetchGroupInvitations('group-1')

      const result = await inv.resendInvitation('group-1', 'inv-1')

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/invitations/inv-1/resend')
      expect(inv.invitations.value).toEqual([updated])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.invitations.resent')
      expect(result).toEqual(updated)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Resend failed'))
      const inv = useInvitations()

      await expect(inv.resendInvitation('group-1', 'inv-1')).rejects.toThrow('Resend failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.invitations.resendFailed')
      expect(inv.isLoading.value).toBe(false)
    })
  })

  describe('revokeInvitation', () => {
    it('removes the invitation from the list and shows a success toast', async () => {
      apiMock.get.mockResolvedValue({
        success: true,
        data: [invitation({ id: 'inv-1' }), invitation({ id: 'inv-2', email: 'carol@example.com' })],
      })
      apiMock.delete.mockResolvedValue({ success: true })
      const inv = useInvitations()
      await inv.fetchGroupInvitations('group-1')

      await inv.revokeInvitation('group-1', 'inv-1')

      expect(apiMock.delete).toHaveBeenCalledWith('/groups/group-1/invitations/inv-1')
      expect(inv.invitations.value.map(i => i.id)).toEqual(['inv-2'])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.invitations.revoked')
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Revoke failed'))
      const inv = useInvitations()

      await expect(inv.revokeInvitation('group-1', 'inv-1')).rejects.toThrow('Revoke failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.invitations.revokeFailed')
      expect(inv.isLoading.value).toBe(false)
    })
  })

  describe('validateInvitationToken', () => {
    it('returns the validation data', async () => {
      const data = validateResponse()
      apiMock.get.mockResolvedValue({ success: true, data })
      const inv = useInvitations()

      const result = await inv.validateInvitationToken('token-123')

      expect(apiMock.get).toHaveBeenCalledWith('/invitations/validate', { token: 'token-123' })
      expect(result).toEqual(data)
    })

    it('propagates API errors without showing a toast', async () => {
      apiMock.get.mockRejectedValue(new Error('Invalid token'))
      const inv = useInvitations()

      await expect(inv.validateInvitationToken('token-123')).rejects.toThrow('Invalid token')

      expect(notificationsMock.showError).not.toHaveBeenCalled()
      expect(inv.isLoading.value).toBe(false)
    })
  })

  describe('acceptInvitation', () => {
    it('shows a success toast and returns the response envelope', async () => {
      apiMock.post.mockResolvedValue({ success: true })
      const inv = useInvitations()

      const result = await inv.acceptInvitation(acceptRequest)

      expect(apiMock.post).toHaveBeenCalledWith('/invitations/accept', acceptRequest)
      expect(tMock).toHaveBeenCalledWith('toasts.invitations.accountCreated')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.invitations.accountCreated')
      expect(result).toEqual({ success: true })
    })

    it('shows the extracted backend message and re-throws when the API returns a structured error', async () => {
      const error = structuredError('Token expired')
      apiMock.post.mockRejectedValue(error)
      const inv = useInvitations()

      await expect(inv.acceptInvitation(acceptRequest)).rejects.toBe(error)

      expect(notificationsMock.showError).toHaveBeenCalledWith('Token expired')
      expect(inv.isLoading.value).toBe(false)
    })

    it('shows the fallback toast and re-throws when the API fails without a structured error', async () => {
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const inv = useInvitations()

      await expect(inv.acceptInvitation(acceptRequest)).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.invitations.accountCreateFailed')
      expect(inv.isLoading.value).toBe(false)
    })
  })

  describe('fetchPendingInvitations', () => {
    it('stores the pending users and returns them', async () => {
      const pending = [pendingUser(), pendingUser({ email: 'carol@example.com' })]
      apiMock.get.mockResolvedValue({ success: true, data: pending })
      const inv = useInvitations()

      const result = await inv.fetchPendingInvitations()

      expect(apiMock.get).toHaveBeenCalledWith('/users/pending')
      expect(inv.pendingUsers.value).toEqual(pending)
      expect(result).toEqual(pending)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Load failed'))
      const inv = useInvitations()

      await expect(inv.fetchPendingInvitations()).rejects.toThrow('Load failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.invitations.pendingLoadFailed')
      expect(inv.isLoading.value).toBe(false)
    })
  })
})
