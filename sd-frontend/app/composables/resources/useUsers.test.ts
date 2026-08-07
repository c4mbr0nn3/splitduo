import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import useUsers from './useUsers'
import { apiMock } from '~/composables/api/base.mock'
import type { User, ImportStatus, UserStats, UpdateUserRequest, ChangePasswordRequest } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useUsers.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const user = (overrides: Partial<User> = {}): User => ({
  id: 'user-1',
  email: 'alice@example.com',
  firstName: 'Alice',
  globalRoleId: 1,
  createdAt: 0,
  updatedAt: 0,
  twoFactorEnabled: false,
  settings: { theme: 'light', uiLanguage: 'en' },
  ...overrides,
})

const importStatus = (overrides: Partial<ImportStatus> = {}): ImportStatus => ({
  id: 'import-1',
  fileName: 'expenses.csv',
  fileHash: 'abc123',
  importStatusId: 1,
  importTypeId: 1,
  recordsCount: 10,
  errorDetails: '',
  importDate: '2026-01-01',
  createdAt: 0,
  updatedAt: 0,
  ...overrides,
})

const userStats = (overrides: Partial<UserStats> = {}): UserStats => ({
  totalGroups: 3,
  youOwe: 25,
  youreOwed: 40,
  ...overrides,
})

const updateUserRequest: UpdateUserRequest = { firstName: 'Alicia' }
const changePasswordRequest: ChangePasswordRequest = {
  currentPassword: 'old-pass',
  newPassword: 'new-pass',
  confirmPassword: 'new-pass',
}

describe('useUsers', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchUsers', () => {
    it('stores the users from the response and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [user()] })
      const users = useUsers()

      await users.fetchUsers()

      expect(apiMock.get).toHaveBeenCalledWith('/users')
      expect(users.users.value).toEqual([user()])
      expect(users.isLoading.value).toBe(false)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Network down'))
      const users = useUsers()

      await expect(users.fetchUsers()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.loadFailed')
      expect(users.isLoading.value).toBe(false)
    })
  })

  describe('fetchCurrentUser', () => {
    it('stores the user as currentUser, returns it, and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: user() })
      const users = useUsers()

      const result = await users.fetchCurrentUser()

      expect(apiMock.get).toHaveBeenCalledWith('/users/me')
      expect(users.currentUser.value).toEqual(user())
      expect(result).toEqual(user())
      expect(users.isLoading.value).toBe(false)
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Profile failed'))
      const users = useUsers()

      await expect(users.fetchCurrentUser()).rejects.toThrow('Profile failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.profileLoadFailed')
      expect(users.isLoading.value).toBe(false)
    })
  })

  describe('updateCurrentUser', () => {
    it('updates currentUser, shows a success toast, and returns the user', async () => {
      apiMock.put.mockResolvedValue({ success: true, data: user({ firstName: 'Alicia' }) })
      const users = useUsers()

      const result = await users.updateCurrentUser(updateUserRequest)

      expect(apiMock.put).toHaveBeenCalledWith('/users/me', updateUserRequest)
      expect(users.currentUser.value).toEqual(user({ firstName: 'Alicia' }))
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.users.profileUpdated')
      expect(result).toEqual(user({ firstName: 'Alicia' }))
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Update failed'))
      const users = useUsers()

      await expect(users.updateCurrentUser(updateUserRequest)).rejects.toThrow('Update failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.profileUpdateFailed')
    })
  })

  describe('changePassword', () => {
    it('shows a success toast after changing the password', async () => {
      apiMock.put.mockResolvedValue({ success: true, data: null })
      const users = useUsers()

      await users.changePassword(changePasswordRequest)

      expect(apiMock.put).toHaveBeenCalledWith('/users/me/password', changePasswordRequest)
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.users.passwordChanged')
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Password failed'))
      const users = useUsers()

      await expect(users.changePassword(changePasswordRequest)).rejects.toThrow('Password failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.passwordChangeFailed')
    })
  })

  describe('fetchUserImports', () => {
    it('stores the imports, returns them, and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [importStatus()] })
      const users = useUsers()

      const result = await users.fetchUserImports()

      expect(apiMock.get).toHaveBeenCalledWith('/users/me/imports')
      expect(users.userImports.value).toEqual([importStatus()])
      expect(result).toEqual([importStatus()])
      expect(users.isLoading.value).toBe(false)
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Imports failed'))
      const users = useUsers()

      await expect(users.fetchUserImports()).rejects.toThrow('Imports failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.importsLoadFailed')
    })
  })

  describe('fetchUserStats', () => {
    it('stores the stats, returns them, and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: userStats() })
      const users = useUsers()

      const result = await users.fetchUserStats()

      expect(apiMock.get).toHaveBeenCalledWith('/users/me/stats')
      expect(users.userStats.value).toEqual(userStats())
      expect(result).toEqual(userStats())
      expect(users.isLoading.value).toBe(false)
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Stats failed'))
      const users = useUsers()

      await expect(users.fetchUserStats()).rejects.toThrow('Stats failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.statsLoadFailed')
    })
  })

  describe('fetchUser', () => {
    it('returns the user from the response', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: user() })
      const users = useUsers()

      const result = await users.fetchUser('user-1')

      expect(apiMock.get).toHaveBeenCalledWith('/users/user-1')
      expect(result).toEqual(user())
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Load failed'))
      const users = useUsers()

      await expect(users.fetchUser('user-1')).rejects.toThrow('Load failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.loadOneFailed')
    })
  })

  describe('updateUser', () => {
    it('updates the user in the list, shows a success toast, and returns the user', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [user(), user({ id: 'user-2', firstName: 'Bob' })] })
      apiMock.put.mockResolvedValue({ success: true, data: user({ firstName: 'Alicia' }) })
      const users = useUsers()
      await users.fetchUsers()

      const result = await users.updateUser('user-1', updateUserRequest)

      expect(apiMock.put).toHaveBeenCalledWith('/users/user-1', updateUserRequest)
      expect(users.users.value[0]).toMatchObject({ firstName: 'Alicia' })
      expect(users.users.value[1]).toMatchObject({ firstName: 'Bob' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.users.updated')
      expect(result).toEqual(user({ firstName: 'Alicia' }))
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Update failed'))
      const users = useUsers()

      await expect(users.updateUser('user-1', updateUserRequest)).rejects.toThrow('Update failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.updateFailed')
    })
  })

  describe('deleteUser', () => {
    it('removes the user from the list and shows a success toast', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [user(), user({ id: 'user-2', firstName: 'Bob' })] })
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const users = useUsers()
      await users.fetchUsers()

      await users.deleteUser('user-1')

      expect(apiMock.delete).toHaveBeenCalledWith('/users/user-1')
      expect(users.users.value).toEqual([user({ id: 'user-2', firstName: 'Bob' })])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.users.deleted')
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const users = useUsers()

      await expect(users.deleteUser('user-1')).rejects.toThrow('Delete failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.deleteFailed')
    })
  })

  describe('changeUserRole', () => {
    it('updates the user in the list, shows a success toast, and returns the user', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [user()] })
      apiMock.put.mockResolvedValue({ success: true, data: user({ globalRoleId: 2 }) })
      const users = useUsers()
      await users.fetchUsers()

      const result = await users.changeUserRole('user-1', 2)

      expect(apiMock.put).toHaveBeenCalledWith('/users/user-1', { globalRole: 2 })
      expect(users.users.value[0]).toMatchObject({ globalRoleId: 2 })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.users.roleUpdated')
      expect(result).toEqual(user({ globalRoleId: 2 }))
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Role failed'))
      const users = useUsers()

      await expect(users.changeUserRole('user-1', 2)).rejects.toThrow('Role failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.roleUpdateFailed')
    })
  })

  describe('revokeUserTokens', () => {
    it('shows a success toast after revoking the tokens', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: null })
      const users = useUsers()

      await users.revokeUserTokens('user-1')

      expect(apiMock.post).toHaveBeenCalledWith('/auth/user-1/revoke')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.users.tokensRevoked')
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Revoke failed'))
      const users = useUsers()

      await expect(users.revokeUserTokens('user-1')).rejects.toThrow('Revoke failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.users.tokensRevokeFailed')
    })
  })
})
