import { describe, it, expect, vi, beforeAll, beforeEach, afterEach } from 'vitest'

import { apiMock } from '~/composables/api/base.mock'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const authMock = vi.hoisted(() => ({
  isGlobalAdmin: { value: true } as { value: boolean },
}))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi and useAuth are auto-imported inside useSystemNotifications.ts; mock
// the composable modules so every API call and the admin gate are controlled
// from the test. useNotifications is mocked to assert that failures stay silent.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('~/composables/auth/useAuth', () => ({ default: () => authMock }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

// useSystemNotifications is a singleton: its module-level refs
// (globalNotifications, globalIsInitialized) and fetchPromise persist across
// tests. Reset the module registry in beforeEach and import a fresh module
// instance so every test starts from the uninitialized state.
describe('useSystemNotifications', () => {
  beforeAll(async () => {
    await import('./useSystemNotifications')
  })

  beforeEach(() => {
    vi.clearAllMocks()
    vi.resetModules()
    authMock.isGlobalAdmin.value = true
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  const updateNotification = {
    type: 'update-available',
    targetKey: '1.2.3',
    payload: { current: '1.2.2', latest: '1.2.3', releaseUrl: 'https://github.com/splitduo/releases/tag/v1.2.3' },
  }

  describe('fetchSystemNotifications', () => {
    it('stores notifications from the API', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [updateNotification] })
      const { default: useSystemNotifications } = await import('./useSystemNotifications')
      const sn = useSystemNotifications()

      await sn.fetchSystemNotifications()

      expect(apiMock.get).toHaveBeenCalledWith('/admin/notifications')
      expect(sn.notifications.value).toEqual([updateNotification])
    })

    it('does not call the API for non-admin users', async () => {
      authMock.isGlobalAdmin.value = false
      const { default: useSystemNotifications } = await import('./useSystemNotifications')
      const sn = useSystemNotifications()

      await sn.fetchSystemNotifications()

      expect(apiMock.get).not.toHaveBeenCalled()
      expect(sn.notifications.value).toEqual([])
    })

    it('fetches only once per session (singleton)', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [updateNotification] })
      const { default: useSystemNotifications } = await import('./useSystemNotifications')
      const sn = useSystemNotifications()

      await sn.fetchSystemNotifications()
      await sn.fetchSystemNotifications()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
    })

    it('deduplicates concurrent calls by sharing the same fetch promise', async () => {
      let resolveFetch: (value: unknown) => void = () => {}
      apiMock.get.mockImplementation(() => new Promise((resolve) => {
        resolveFetch = resolve
      }))
      const { default: useSystemNotifications } = await import('./useSystemNotifications')
      const sn = useSystemNotifications()

      const first = sn.fetchSystemNotifications()
      const second = sn.fetchSystemNotifications()

      expect(apiMock.get).toHaveBeenCalledTimes(1)

      resolveFetch({ success: true, data: [updateNotification] })
      await Promise.all([first, second])

      expect(sn.notifications.value).toEqual([updateNotification])
    })

    it('stays silent on failure: no toast, no throw, list stays empty', async () => {
      apiMock.get.mockRejectedValue(new Error('Network down'))
      const { default: useSystemNotifications } = await import('./useSystemNotifications')
      const sn = useSystemNotifications()

      await expect(sn.fetchSystemNotifications()).resolves.toBeUndefined()

      expect(notificationsMock.showError).not.toHaveBeenCalled()
      expect(sn.notifications.value).toEqual([])
    })
  })

  describe('refetch', () => {
    it('re-fetches even after the singleton was initialized', async () => {
      apiMock.get.mockResolvedValueOnce({ success: true, data: [updateNotification] })
      const { default: useSystemNotifications } = await import('./useSystemNotifications')
      const sn = useSystemNotifications()
      await sn.fetchSystemNotifications()

      await sn.refetch()

      expect(apiMock.get).toHaveBeenCalledTimes(2)
    })
  })

  describe('dismiss', () => {
    it('POSTs the dismissal and removes the notification locally', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [updateNotification] })
      apiMock.post.mockResolvedValue({ success: true, data: null })
      const { default: useSystemNotifications } = await import('./useSystemNotifications')
      const sn = useSystemNotifications()
      await sn.fetchSystemNotifications()

      await sn.dismiss('update-available', '1.2.3')

      expect(apiMock.post).toHaveBeenCalledWith('/admin/notifications/dismiss', {
        type: 'update-available',
        targetKey: '1.2.3',
      })
      expect(sn.notifications.value).toEqual([])
    })

    it('keeps the notification visible when the dismissal fails', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [updateNotification] })
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const { default: useSystemNotifications } = await import('./useSystemNotifications')
      const sn = useSystemNotifications()
      await sn.fetchSystemNotifications()

      await sn.dismiss('update-available', '1.2.3')

      expect(notificationsMock.showError).not.toHaveBeenCalled()
      expect(sn.notifications.value).toEqual([updateNotification])
    })
  })
})
