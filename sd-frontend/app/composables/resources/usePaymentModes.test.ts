import { describe, it, expect, vi, beforeAll, beforeEach, afterEach } from 'vitest'

import { apiMock } from '~/composables/api/base.mock'
import type { PaymentMode } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside usePaymentModes.ts; mock
// the composable modules so every API call and toast is controlled from the
// test. useI18n is auto-imported from 'vue-i18n'; mock the module so `t` is a
// controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const paymentModes: PaymentMode[] = [
  { id: 1, name: 'Cash' },
  { id: 2, name: 'Card' },
]

// usePaymentModes is a singleton: its module-level refs (globalPaymentModes,
// globalIsLoading, globalIsInitialized) and fetchPromise persist across tests.
// Reset the module registry in beforeEach and import a fresh module instance so
// every test starts from the uninitialized state.
//
// The first dynamic import of the module graph pays a one-off transform cost
// that can exceed the default 5000ms test timeout, so warm it up in beforeAll
// (the registry is reset in beforeEach, so tests still get fresh instances).
describe('usePaymentModes', () => {
  beforeAll(async () => {
    await import('./usePaymentModes')
  })

  beforeEach(() => {
    vi.clearAllMocks()
    vi.resetModules()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchPaymentModes', () => {
    it('fetches from the API and stores payment modes on first call', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: paymentModes })
      const { default: usePaymentModes } = await import('./usePaymentModes')
      const upm = usePaymentModes()

      await upm.fetchPaymentModes()

      expect(apiMock.get).toHaveBeenCalledWith('/payment-modes')
      expect(upm.paymentModes.value).toEqual(paymentModes)
      expect(upm.isLoading.value).toBe(false)
    })

    it('returns immediately without calling the API once initialized', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: paymentModes })
      const { default: usePaymentModes } = await import('./usePaymentModes')
      const upm = usePaymentModes()
      await upm.fetchPaymentModes()
      expect(apiMock.get).toHaveBeenCalledTimes(1)

      await upm.fetchPaymentModes()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(upm.paymentModes.value).toEqual(paymentModes)
    })

    it('deduplicates concurrent calls by sharing the same fetch promise', async () => {
      let resolveFetch: (value: unknown) => void = () => {}
      apiMock.get.mockImplementation(() => new Promise((resolve) => {
        resolveFetch = resolve
      }))
      const { default: usePaymentModes } = await import('./usePaymentModes')
      const upm = usePaymentModes()

      const first = upm.fetchPaymentModes()
      const second = upm.fetchPaymentModes()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(upm.isLoading.value).toBe(true)

      resolveFetch({ success: true, data: paymentModes })
      await Promise.all([first, second])

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(upm.paymentModes.value).toEqual(paymentModes)
      expect(upm.isLoading.value).toBe(false)
    })

    it('shows an error toast, re-throws, and resets loading state when the API call fails', async () => {
      // The auto-init on first use fires a fetch whose promise is discarded by
      // the composable; give it a non-throwing response so that discarded
      // promise resolves instead of surfacing as an unhandled rejection.
      apiMock.get.mockResolvedValueOnce({ success: false, data: null })
      apiMock.get.mockRejectedValueOnce(new Error('Network down'))
      const { default: usePaymentModes } = await import('./usePaymentModes')
      const upm = usePaymentModes()

      // Let the auto-init fetch settle so fetchPromise is cleared and the next
      // call issues a fresh request instead of reusing the in-flight promise.
      await new Promise(resolve => setTimeout(resolve, 0))

      await expect(upm.fetchPaymentModes()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.paymentModes.loadFailed')
      expect(upm.isLoading.value).toBe(false)
    })

    it('allows a retry after a failed fetch', async () => {
      // The auto-init on first use fires a fetch whose promise is discarded by
      // the composable; give it a non-throwing response so that discarded
      // promise resolves instead of surfacing as an unhandled rejection.
      apiMock.get.mockResolvedValueOnce({ success: false, data: null })
      apiMock.get.mockRejectedValueOnce(new Error('Network down'))
      apiMock.get.mockResolvedValueOnce({ success: true, data: paymentModes })
      const { default: usePaymentModes } = await import('./usePaymentModes')
      const upm = usePaymentModes()

      // Let the auto-init fetch settle so fetchPromise is cleared and the next
      // call issues a fresh request instead of reusing the in-flight promise.
      await new Promise(resolve => setTimeout(resolve, 0))

      await expect(upm.fetchPaymentModes()).rejects.toThrow('Network down')
      await upm.fetchPaymentModes()

      expect(apiMock.get).toHaveBeenCalledTimes(3)
      expect(upm.paymentModes.value).toEqual(paymentModes)
    })
  })

  describe('getPaymentModeName', () => {
    it('returns the payment mode name for a valid id', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: paymentModes })
      const { default: usePaymentModes } = await import('./usePaymentModes')
      const upm = usePaymentModes()
      await upm.fetchPaymentModes()

      expect(upm.getPaymentModeName(1)).toBe('Cash')
      expect(upm.getPaymentModeName(2)).toBe('Card')
    })

    it('returns \'Unknown\' for an invalid id', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: paymentModes })
      const { default: usePaymentModes } = await import('./usePaymentModes')
      const upm = usePaymentModes()
      await upm.fetchPaymentModes()

      expect(upm.getPaymentModeName(999)).toBe('Unknown')
    })
  })

  describe('auto-initialization', () => {
    it('triggers a fetch on first use when not initialized', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: paymentModes })
      const { default: usePaymentModes } = await import('./usePaymentModes')

      const upm = usePaymentModes()

      expect(apiMock.get).toHaveBeenCalledWith('/payment-modes')
      await upm.fetchPaymentModes()
      expect(upm.paymentModes.value).toEqual(paymentModes)
    })

    it('does not auto-fetch again on subsequent uses once initialized', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: paymentModes })
      const { default: usePaymentModes } = await import('./usePaymentModes')
      const first = usePaymentModes()
      await first.fetchPaymentModes()
      expect(apiMock.get).toHaveBeenCalledTimes(1)

      const second = usePaymentModes()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(second.paymentModes.value).toEqual(paymentModes)
    })

    it('does not auto-fetch when a fetch is already in flight', async () => {
      let resolveFetch: (value: unknown) => void = () => {}
      apiMock.get.mockImplementation(() => new Promise((resolve) => {
        resolveFetch = resolve
      }))
      const { default: usePaymentModes } = await import('./usePaymentModes')

      const first = usePaymentModes()
      expect(apiMock.get).toHaveBeenCalledTimes(1)

      const second = usePaymentModes()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      resolveFetch({ success: true, data: paymentModes })
      await first.fetchPaymentModes()
      expect(second.paymentModes.value).toEqual(paymentModes)
    })
  })
})
