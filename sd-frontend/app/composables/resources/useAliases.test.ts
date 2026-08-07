import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import useAliases from './useAliases'
import { apiMock } from '~/composables/api/base.mock'
import type { Alias, Group } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useAliases.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const alias = (overrides: Partial<Alias> = {}): Alias => ({
  id: 'alias-1',
  name: 'Family',
  groupId: 'group-1',
  isSingleton: false,
  createdAt: 1700000000000,
  updatedAt: 1700000000000,
  ...overrides,
})

const group = (overrides: Partial<Group> = {}): Group => ({
  id: 'group-1',
  name: 'Family',
  createdByUserId: 'user-1',
  memberCount: 3,
  createdAt: 1700000000000,
  updatedAt: 1700000000000,
  netBalance: 0,
  useAliases: true,
  aliasSetupFinalized: true,
  ...overrides,
})

describe('useAliases', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchAliases', () => {
    it('stores the aliases and returns them', async () => {
      const aliases = [alias({ id: 'alias-1' }), alias({ id: 'alias-2', name: 'Friends' })]
      apiMock.get.mockResolvedValue({ success: true, data: aliases })
      const al = useAliases()

      const result = await al.fetchAliases('group-1')

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/aliases')
      expect(al.aliases.value).toEqual(aliases)
      expect(result).toEqual(aliases)
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const al = useAliases()

      const result = await al.fetchAliases('')

      expect(apiMock.get).not.toHaveBeenCalled()
      expect(result).toBeUndefined()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Load failed'))
      const al = useAliases()

      await expect(al.fetchAliases('group-1')).rejects.toThrow('Load failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.aliases.loadFailed')
      expect(al.isLoading.value).toBe(false)
    })
  })

  describe('listAliases', () => {
    it('delegates to fetchAliases and returns the fetched aliases', async () => {
      const aliases = [alias()]
      apiMock.get.mockResolvedValue({ success: true, data: aliases })
      const al = useAliases()

      const result = await al.listAliases('group-1')

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/aliases')
      expect(al.aliases.value).toEqual(aliases)
      expect(result).toEqual(aliases)
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const al = useAliases()

      const result = await al.listAliases('')

      expect(apiMock.get).not.toHaveBeenCalled()
      expect(result).toBeUndefined()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Load failed'))
      const al = useAliases()

      await expect(al.listAliases('group-1')).rejects.toThrow('Load failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.aliases.loadFailed')
    })
  })

  describe('createAlias', () => {
    it('shows a success toast and returns the created alias', async () => {
      const created = alias({ id: 'alias-1', name: 'Family' })
      apiMock.post.mockResolvedValue({ success: true, data: created })
      const al = useAliases()

      const result = await al.createAlias('group-1', { name: 'Family' })

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/aliases', { name: 'Family' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.aliases.created')
      expect(result).toEqual(created)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Create failed'))
      const al = useAliases()

      await expect(al.createAlias('group-1', { name: 'Family' })).rejects.toThrow('Create failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.aliases.createFailed')
      expect(al.isLoading.value).toBe(false)
    })
  })

  describe('updateAlias', () => {
    it('shows a success toast and returns the updated alias', async () => {
      const updated = alias({ id: 'alias-1', name: 'Family (renamed)' })
      apiMock.put.mockResolvedValue({ success: true, data: updated })
      const al = useAliases()

      const result = await al.updateAlias('alias-1', { name: 'Family (renamed)' })

      expect(apiMock.put).toHaveBeenCalledWith('/aliases/alias-1', { name: 'Family (renamed)' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.aliases.updated')
      expect(result).toEqual(updated)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Update failed'))
      const al = useAliases()

      await expect(al.updateAlias('alias-1', { name: 'Family' })).rejects.toThrow('Update failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.aliases.updateFailed')
      expect(al.isLoading.value).toBe(false)
    })
  })

  describe('deleteAlias', () => {
    it('shows a success toast after deleting', async () => {
      apiMock.delete.mockResolvedValue({ success: true })
      const al = useAliases()

      await al.deleteAlias('alias-1')

      expect(apiMock.delete).toHaveBeenCalledWith('/aliases/alias-1')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.aliases.deleted')
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const al = useAliases()

      await expect(al.deleteAlias('alias-1')).rejects.toThrow('Delete failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.aliases.deleteFailed')
      expect(al.isLoading.value).toBe(false)
    })
  })

  describe('assignMember', () => {
    it('shows a success toast and returns the updated alias', async () => {
      const updated = alias({ id: 'alias-1', members: [{ id: 'user-2', firstName: 'Carol' }] })
      apiMock.post.mockResolvedValue({ success: true, data: updated })
      const al = useAliases()

      const result = await al.assignMember('alias-1', { userId: 'user-2' })

      expect(apiMock.post).toHaveBeenCalledWith('/aliases/alias-1/members', { userId: 'user-2' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.aliases.memberAssigned')
      expect(result).toEqual(updated)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Assign failed'))
      const al = useAliases()

      await expect(al.assignMember('alias-1', { userId: 'user-2' })).rejects.toThrow('Assign failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.aliases.memberAssignFailed')
      expect(al.isLoading.value).toBe(false)
    })
  })

  describe('removeMember', () => {
    it('shows a success toast after removing the member', async () => {
      apiMock.delete.mockResolvedValue({ success: true })
      const al = useAliases()

      await al.removeMember('alias-1', 'user-2')

      expect(apiMock.delete).toHaveBeenCalledWith('/aliases/alias-1/members/user-2')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.aliases.memberRemoved')
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Remove failed'))
      const al = useAliases()

      await expect(al.removeMember('alias-1', 'user-2')).rejects.toThrow('Remove failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.aliases.memberRemoveFailed')
      expect(al.isLoading.value).toBe(false)
    })
  })

  describe('finalizeAliasSetup', () => {
    it('shows a success toast and returns the finalized group', async () => {
      const finalized = group({ aliasSetupFinalized: true })
      apiMock.post.mockResolvedValue({ success: true, data: finalized })
      const al = useAliases()

      const result = await al.finalizeAliasSetup('group-1')

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/aliases/finalize')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.aliases.finalized')
      expect(result).toEqual(finalized)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Finalize failed'))
      const al = useAliases()

      await expect(al.finalizeAliasSetup('group-1')).rejects.toThrow('Finalize failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.aliases.finalizeFailed')
      expect(al.isLoading.value).toBe(false)
    })
  })
})
