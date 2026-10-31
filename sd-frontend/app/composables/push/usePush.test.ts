import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import usePush from './usePush'
import { apiMock } from '~/composables/api/base.mock'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))
const authMock = vi.hoisted(() => ({
  isAuthenticated: { value: true },
}))

// useApi / useI18n / useNotifications / useAuth are auto-imported inside
// usePush.ts; mock at those boundaries so every API call and toast is
// controlled from the test.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))
vi.mock('~/composables/auth/useAuth', () => ({ default: () => authMock }))

describe('usePush', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    authMock.isAuthenticated.value = true
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('removeSubscription', () => {
    it('deletes by endpoint, shows a success toast, and returns true', async () => {
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const push = usePush()

      const result = await push.removeSubscription('https://push.example/abc')

      expect(apiMock.delete).toHaveBeenCalledWith('/push/subscriptions', { endpoint: 'https://push.example/abc' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('push.subscriptionRemoved')
      expect(result).toBe(true)
    })

    it('shows an error toast and returns false when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const push = usePush()

      const result = await push.removeSubscription('https://push.example/abc')

      expect(notificationsMock.showError).toHaveBeenCalledWith('push.subscriptionRemoveFailed')
      expect(result).toBe(false)
    })
  })

  describe('fetchSubscriptions', () => {
    it('returns the subscription list on success', async () => {
      const items = [
        {
          id: 'abc',
          endpoint: 'https://push.example/abc',
          userAgent: 'Mozilla/5.0',
          createdAt: 1735689600,
          lastUsedAt: null,
        },
      ]
      apiMock.get.mockResolvedValue({ success: true, data: items })
      const push = usePush()

      const result = await push.fetchSubscriptions()

      expect(apiMock.get).toHaveBeenCalledWith('/push/subscriptions')
      expect(result).toEqual(items)
    })

    it('returns an empty list on failure without toasting', async () => {
      apiMock.get.mockRejectedValue(new Error('Network error'))
      const push = usePush()

      const result = await push.fetchSubscriptions()

      expect(result).toEqual([])
      expect(notificationsMock.showError).not.toHaveBeenCalled()
    })
  })

  describe('shouldShowPushOptIn', () => {
    it('is false when the user previously dismissed the prompt', async () => {
      localStorage.setItem('push-prompt-dismissed', '1')
      apiMock.get.mockRejectedValue(Object.assign(new Error('503'), { statusCode: 503 }))
      const push = usePush()
      await push.checkConfiguration()

      expect(push.shouldShowPushOptIn()).toBe(false)
    })

    it('is false when the server has push disabled (isConfigured false)', async () => {
      apiMock.get.mockRejectedValue(Object.assign(new Error('503'), { statusCode: 503 }))
      const push = usePush()
      await push.checkConfiguration()

      expect(push.shouldShowPushOptIn()).toBe(false)
    })

    it('is true for an authenticated user with push supported, configured, and undecided', async () => {
      // happy-dom doesn't expose the Push API — stub the feature checks
      // (only their presence matters for isSupported).
      vi.stubGlobal('Notification', { permission: 'default' })
      Object.defineProperty(navigator, 'serviceWorker', {
        value: { ready: Promise.resolve({}) },
        configurable: true,
      })
      Object.defineProperty(window, 'PushManager', { value: {}, configurable: true })
      apiMock.get.mockResolvedValue({ success: true, data: { publicKey: 'key' } })
      const push = usePush()
      await push.checkConfiguration()

      expect(push.shouldShowPushOptIn()).toBe(true)
    })

    it('is false when the user is not authenticated', async () => {
      authMock.isAuthenticated.value = false
      vi.stubGlobal('Notification', { permission: 'granted' })
      Object.defineProperty(navigator, 'serviceWorker', {
        value: { ready: Promise.resolve({}) },
        configurable: true,
      })
      Object.defineProperty(window, 'PushManager', { value: {}, configurable: true })
      const push = usePush()

      // Even with permission already granted (otherwise ineligible), the
      // auth gate wins — proves the gate is evaluated.
      expect(push.shouldShowPushOptIn()).toBe(false)
    })
  })

  // happy-dom has no Push API — stub the browser subscription plumbing so
  // getRegistrationSubscription()/unsubscribe() behave predictably.
  const stubPushSupport = (getSubscriptionImpl: () => Promise<PushSubscription | null>) => {
    vi.stubGlobal('Notification', { permission: 'granted' })
    Object.defineProperty(navigator, 'serviceWorker', {
      value: { ready: Promise.resolve({ pushManager: { getSubscription: getSubscriptionImpl } }) },
      configurable: true,
    })
    Object.defineProperty(window, 'PushManager', { value: {}, configurable: true })
  }

  const fakeSubscription = {
    endpoint: 'https://push.example/abc',
    unsubscribe: vi.fn().mockResolvedValue(true),
    toJSON: () => ({
      endpoint: 'https://push.example/abc',
      expirationTime: null,
      keys: { p256dh: 'k', auth: 'a' },
    }),
  } as unknown as PushSubscription

  describe('subscribe', () => {
    it('shows the notConfigured toast and skips requestPermission when the key fetch fails (isConfigured null → false)', async () => {
      // isConfigured is singleton state shared across tests (earlier
      // checkConfiguration() calls leave it non-null) — reload the module to
      // start from the genuine in-flight state (null).
      vi.resetModules()
      const { default: freshUsePush } = await import('./usePush')
      stubPushSupport(() => Promise.resolve(null))
      apiMock.get.mockRejectedValue(Object.assign(new Error('503'), { statusCode: 503 }))
      const requestPermission = vi.fn()
      vi.stubGlobal('Notification', { permission: 'granted', requestPermission })
      const push = freshUsePush()

      const result = await push.subscribe()

      expect(result).toBe(false)
      expect(requestPermission).not.toHaveBeenCalled()
      expect(notificationsMock.showError).toHaveBeenCalledWith('push.settings.notConfigured')
    })
  })

  // Full subscribe flow stubbing: VAPID fetch succeeds, permission granted,
  // pushManager.subscribe() yields the fake subscription. The POST outcome is
  // injected so each test can reject/resolve the save independently.
  // vi.resetModules() + dynamic import starts the singleton state clean —
  // earlier tests leave isConfigured/isSubscribed non-default.
  const stubSubscribeFlow = async (postImpl: () => Promise<unknown>) => {
    vi.resetModules()
    const pushManager = {
      getSubscription: () => Promise.resolve(null),
      subscribe: vi.fn().mockResolvedValue(fakeSubscription),
    }
    vi.stubGlobal('Notification', {
      permission: 'default',
      requestPermission: vi.fn().mockResolvedValue('granted'),
    })
    Object.defineProperty(navigator, 'serviceWorker', {
      value: { ready: Promise.resolve({ pushManager }) },
      configurable: true,
    })
    Object.defineProperty(window, 'PushManager', { value: {}, configurable: true })
    apiMock.get.mockResolvedValue({ success: true, data: { publicKey: 'key' } })
    apiMock.post.mockImplementation(postImpl)
    const { default: freshUsePush } = await import('./usePush')
    return { push: freshUsePush(), pushManager }
  }

  describe('subscribe save outcome', () => {
    it('on 409 does not unsubscribe, shows the shared-browser message, and stays unsubscribed', async () => {
      const { push, pushManager } = await stubSubscribeFlow(() =>
        Promise.reject(Object.assign(new Error('409'), { statusCode: 409 })))

      const result = await push.subscribe()

      expect(pushManager.subscribe).toHaveBeenCalledOnce()
      expect(fakeSubscription.unsubscribe).not.toHaveBeenCalled()
      expect(notificationsMock.showError).toHaveBeenCalledWith('push.subscriptionInUse')
      expect(result).toBe(false)
      expect(push.isSubscribed.value).toBe(false)
    })

    it('on other save failures unsubscribes and shows the generic error', async () => {
      const { push, pushManager } = await stubSubscribeFlow(() =>
        Promise.reject(Object.assign(new Error('500'), { statusCode: 500 })))

      const result = await push.subscribe()

      expect(pushManager.subscribe).toHaveBeenCalledOnce()
      expect(fakeSubscription.unsubscribe).toHaveBeenCalledTimes(1)
      expect(notificationsMock.showError).toHaveBeenCalledWith('push.subscribeFailed')
      expect(result).toBe(false)
      expect(push.isSubscribed.value).toBe(false)
    })

    it('on success shows the enabled toast and marks subscribed', async () => {
      const { push, pushManager } = await stubSubscribeFlow(() =>
        Promise.resolve({ success: true, data: null }))

      const result = await push.subscribe()

      expect(pushManager.subscribe).toHaveBeenCalledOnce()
      expect(fakeSubscription.unsubscribe).not.toHaveBeenCalled()
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('push.subscribed')
      expect(result).toBe(true)
      expect(push.isSubscribed.value).toBe(true)
    })
  })

  describe('unsubscribe', () => {
    it('unsubscribes and deletes the server record when a browser subscription exists', async () => {
      stubPushSupport(() => Promise.resolve(fakeSubscription))
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const push = usePush()

      const result = await push.unsubscribe()

      expect(fakeSubscription.unsubscribe).toHaveBeenCalled()
      expect(apiMock.delete).toHaveBeenCalledWith('/push/subscriptions', { endpoint: 'https://push.example/abc' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('push.unsubscribed')
      expect(push.isSubscribed.value).toBe(false)
      expect(result).toBe(true)
    })

    it('skips the backend DELETE when no browser subscription exists', async () => {
      stubPushSupport(() => Promise.resolve(null))
      const push = usePush()

      const result = await push.unsubscribe()

      expect(apiMock.delete).not.toHaveBeenCalled()
      expect(push.isSubscribed.value).toBe(false)
      expect(result).toBe(true)
    })
  })

  describe('init', () => {
    it('reflects the existing browser subscription eagerly (isSubscribed true)', async () => {
      stubPushSupport(() => Promise.resolve(fakeSubscription))
      apiMock.get.mockResolvedValue({ success: true, data: { publicKey: 'key' } })
      const push = usePush()

      push.init()
      await vi.waitFor(() => expect(push.isSubscribed.value).toBe(true))
      expect(apiMock.get).toHaveBeenCalledWith('/push/vapid-public-key')
    })

    it('reflects no browser subscription eagerly (isSubscribed set to false)', async () => {
      stubPushSupport(() => Promise.resolve(null))
      const push = usePush()

      push.init()
      // The eager subscription query always assigns (true or false) — wait
      // for it to land.
      await vi.waitFor(() => expect(push.isSubscribed.value).toBe(false))
      expect(apiMock.get).toHaveBeenCalledWith('/push/vapid-public-key')
    })
  })
})
