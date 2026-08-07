import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import useGroups from './useGroups'
import { apiMock } from '~/composables/api/base.mock'
import type { Group, GroupMember, CreateGroupRequest, UpdateGroupRequest, AddGroupMemberRequest } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useGroups.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const group = (overrides: Partial<Group> = {}): Group => ({
  id: 'group-1',
  name: 'Trip to Rome',
  createdByUserId: 'user-1',
  memberCount: 2,
  createdAt: 0,
  updatedAt: 0,
  netBalance: 0,
  useAliases: false,
  aliasSetupFinalized: false,
  ...overrides,
})

const member = (overrides: Partial<GroupMember> = {}): GroupMember => ({
  groupId: 'group-1',
  userId: 'user-2',
  user: { id: 'user-2', email: 'bob@example.com', firstName: 'Bob' },
  role: 'Member',
  joinedAt: 0,
  ...overrides,
})

const createGroupRequest: CreateGroupRequest = { name: 'Trip to Rome' }
const updateGroupRequest: UpdateGroupRequest = { name: 'Trip to Rome 2026', description: 'Updated' }
const addMemberRequest: AddGroupMemberRequest = { userEmail: 'bob@example.com' }

describe('useGroups', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchGroups', () => {
    it('stores the groups from the response and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [group()] })
      const groups = useGroups()

      await groups.fetchGroups({ page: 1 })

      expect(apiMock.get).toHaveBeenCalledWith('/groups', { page: 1 })
      expect(groups.groups.value).toEqual([group()])
      expect(groups.isLoading.value).toBe(false)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Network down'))
      const groups = useGroups()

      await expect(groups.fetchGroups()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.loadFailed')
      expect(groups.isLoading.value).toBe(false)
    })
  })

  describe('createGroup', () => {
    it('appends the new group, shows a success toast, and returns the group', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: group() })
      const groups = useGroups()

      const result = await groups.createGroup(createGroupRequest)

      expect(apiMock.post).toHaveBeenCalledWith('/groups', createGroupRequest)
      expect(groups.groups.value).toEqual([group()])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.groups.created')
      expect(result).toEqual(group())
      expect(groups.isLoading.value).toBe(false)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Create failed'))
      const groups = useGroups()

      await expect(groups.createGroup(createGroupRequest)).rejects.toThrow('Create failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.createFailed')
      expect(groups.isLoading.value).toBe(false)
    })
  })

  describe('fetchGroup', () => {
    it('stores the group as currentGroup, returns it, and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: group() })
      const groups = useGroups()

      const result = await groups.fetchGroup('group-1')

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1')
      expect(groups.currentGroup.value).toEqual(group())
      expect(result).toEqual(group())
      expect(groups.isLoading.value).toBe(false)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Details failed'))
      const groups = useGroups()

      await expect(groups.fetchGroup('group-1')).rejects.toThrow('Details failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.detailsLoadFailed')
      expect(groups.isLoading.value).toBe(false)
    })
  })

  describe('updateGroup', () => {
    it('updates the group in the list and as currentGroup, shows a success toast, and returns the group', async () => {
      apiMock.get
        .mockResolvedValueOnce({ success: true, data: [group()] })
        .mockResolvedValueOnce({ success: true, data: group() })
      apiMock.put.mockResolvedValue({ success: true, data: group({ name: 'Trip to Rome 2026' }) })
      const groups = useGroups()
      await groups.fetchGroups()
      await groups.fetchGroup('group-1')

      const result = await groups.updateGroup('group-1', updateGroupRequest)

      expect(apiMock.put).toHaveBeenCalledWith('/groups/group-1', { name: 'Trip to Rome 2026', description: 'Updated' })
      expect(groups.groups.value[0]).toMatchObject({ name: 'Trip to Rome 2026' })
      expect(groups.currentGroup.value).toMatchObject({ name: 'Trip to Rome 2026' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.groups.updated')
      expect(result).toEqual(group({ name: 'Trip to Rome 2026' }))
      expect(groups.isLoading.value).toBe(false)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Update failed'))
      const groups = useGroups()

      await expect(groups.updateGroup('group-1', updateGroupRequest)).rejects.toThrow('Update failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.updateFailed')
      expect(groups.isLoading.value).toBe(false)
    })
  })

  describe('deleteGroup', () => {
    it('removes the group from the list, clears currentGroup when it matches, and shows a success toast', async () => {
      apiMock.get
        .mockResolvedValueOnce({ success: true, data: [group(), group({ id: 'group-2', name: 'Other' })] })
        .mockResolvedValueOnce({ success: true, data: group() })
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const groups = useGroups()
      await groups.fetchGroups()
      await groups.fetchGroup('group-1')

      await groups.deleteGroup('group-1')

      expect(apiMock.delete).toHaveBeenCalledWith('/groups/group-1')
      expect(groups.groups.value).toEqual([group({ id: 'group-2', name: 'Other' })])
      expect(groups.currentGroup.value).toBeNull()
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.groups.deleted')
      expect(groups.isLoading.value).toBe(false)
    })

    it('keeps currentGroup when a different group is deleted', async () => {
      apiMock.get
        .mockResolvedValueOnce({ success: true, data: [group(), group({ id: 'group-2', name: 'Other' })] })
        .mockResolvedValueOnce({ success: true, data: group() })
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const groups = useGroups()
      await groups.fetchGroups()
      await groups.fetchGroup('group-1')

      await groups.deleteGroup('group-2')

      expect(groups.groups.value).toEqual([group()])
      expect(groups.currentGroup.value).toEqual(group())
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const groups = useGroups()

      await expect(groups.deleteGroup('group-1')).rejects.toThrow('Delete failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.deleteFailed')
      expect(groups.isLoading.value).toBe(false)
    })
  })

  describe('fetchGroupMembers', () => {
    it('returns the members from the response', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [member()] })
      const groups = useGroups()

      const result = await groups.fetchGroupMembers('group-1')

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/members')
      expect(result).toEqual([member()])
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Members failed'))
      const groups = useGroups()

      await expect(groups.fetchGroupMembers('group-1')).rejects.toThrow('Members failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.membersLoadFailed')
    })
  })

  describe('addGroupMember', () => {
    it('shows a success toast and returns the added member', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: member() })
      const groups = useGroups()

      const result = await groups.addGroupMember('group-1', addMemberRequest)

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/members', addMemberRequest)
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.groups.memberAdded')
      expect(result).toEqual(member())
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Add failed'))
      const groups = useGroups()

      await expect(groups.addGroupMember('group-1', addMemberRequest)).rejects.toThrow('Add failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.memberAddFailed')
    })
  })

  describe('removeGroupMember', () => {
    it('shows a success toast after removing the member', async () => {
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const groups = useGroups()

      await groups.removeGroupMember('group-1', 'user-2')

      expect(apiMock.delete).toHaveBeenCalledWith('/groups/group-1/members/user-2')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.groups.memberRemoved')
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Remove failed'))
      const groups = useGroups()

      await expect(groups.removeGroupMember('group-1', 'user-2')).rejects.toThrow('Remove failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.memberRemoveFailed')
    })
  })

  describe('changeMemberRole', () => {
    it('shows a success toast and returns the updated member', async () => {
      apiMock.put.mockResolvedValue({ success: true, data: member({ role: 'Admin' }) })
      const groups = useGroups()

      const result = await groups.changeMemberRole('group-1', 'user-2', 'Admin')

      expect(apiMock.put).toHaveBeenCalledWith('/groups/group-1/members/user-2/role', { role: 'Admin' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.groups.memberRoleUpdated')
      expect(result).toEqual(member({ role: 'Admin' }))
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Role failed'))
      const groups = useGroups()

      await expect(groups.changeMemberRole('group-1', 'user-2', 'Admin')).rejects.toThrow('Role failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.groups.memberRoleUpdateFailed')
    })
  })
})
