import { describe, it, expect, vi, beforeAll, beforeEach, afterEach } from 'vitest'

import { apiMock } from '~/composables/api/base.mock'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi is auto-imported inside useAiStatus.ts; mock the composable module so
// every API call is controlled from the test. useNotifications is mocked to
// assert that failures stay silent (no toast).
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

// useAiStatus is a singleton: its module-level refs (globalIsAiEnabled,
// globalIsInitialized) and fetchPromise persist across tests. Reset the module
// registry in beforeEach and import a fresh module instance so every test
// starts from the uninitialized state.
//
// The first dynamic import of the module graph pays a one-off transform cost
// that can exceed the default 5000ms test timeout, so warm it up in beforeAll
// (the registry is reset in beforeEach, so tests still get fresh instances).
describe('useAiStatus', () => {
  beforeAll(async () => {
    await import('./useAiStatus')
  })

  beforeEach(() => {
    vi.clearAllMocks()
    vi.resetModules()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchAiStatus', () => {
    it('enables the AI feature when the API reports it enabled', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: { enabled: true } })
      const { default: useAiStatus } = await import('./useAiStatus')
      const ai = useAiStatus()

      await ai.fetchAiStatus()

      expect(apiMock.get).toHaveBeenCalledWith('/ai/status')
      expect(ai.isAiEnabled.value).toBe(true)
    })

    it('keeps the AI feature disabled when the API reports it disabled', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: { enabled: false } })
      const { default: useAiStatus } = await import('./useAiStatus')
      const ai = useAiStatus()

      await ai.fetchAiStatus()

      expect(ai.isAiEnabled.value).toBe(false)
    })

    it('returns immediately without calling the API once initialized', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: { enabled: true } })
      const { default: useAiStatus } = await import('./useAiStatus')
      const ai = useAiStatus()
      await ai.fetchAiStatus()
      expect(apiMock.get).toHaveBeenCalledTimes(1)

      await ai.fetchAiStatus()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(ai.isAiEnabled.value).toBe(true)
    })

    it('deduplicates concurrent calls by sharing the same fetch promise', async () => {
      let resolveFetch: (value: unknown) => void = () => {}
      apiMock.get.mockImplementation(() => new Promise((resolve) => {
        resolveFetch = resolve
      }))
      const { default: useAiStatus } = await import('./useAiStatus')
      const ai = useAiStatus()

      const first = ai.fetchAiStatus()
      const second = ai.fetchAiStatus()

      expect(apiMock.get).toHaveBeenCalledTimes(1)

      resolveFetch({ success: true, data: { enabled: true } })
      await Promise.all([first, second])

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(ai.isAiEnabled.value).toBe(true)
    })

    it('stays silent on failure: no toast, no throw, feature stays hidden', async () => {
      // The auto-init on first use fires a fetch whose promise is discarded by
      // the composable; give it a non-throwing response so that discarded
      // promise resolves instead of surfacing as an unhandled rejection.
      apiMock.get.mockResolvedValueOnce({ success: false, data: null })
      apiMock.get.mockRejectedValueOnce(new Error('Network down'))
      const { default: useAiStatus } = await import('./useAiStatus')
      const ai = useAiStatus()

      // Let the auto-init fetch settle so fetchPromise is cleared and the next
      // call issues a fresh request instead of reusing the in-flight promise.
      await new Promise(resolve => setTimeout(resolve, 0))

      await expect(ai.fetchAiStatus()).resolves.toBeUndefined()

      expect(notificationsMock.showError).not.toHaveBeenCalled()
      expect(ai.isAiEnabled.value).toBe(false)
    })

    it('marks the status as initialized even when the fetch fails', async () => {
      // The auto-init on first use fires a fetch whose promise is discarded by
      // the composable; give it a non-throwing response so that discarded
      // promise resolves instead of surfacing as an unhandled rejection.
      apiMock.get.mockResolvedValueOnce({ success: false, data: null })
      apiMock.get.mockRejectedValueOnce(new Error('Network down'))
      const { default: useAiStatus } = await import('./useAiStatus')
      const ai = useAiStatus()

      // Let the auto-init fetch settle so fetchPromise is cleared and the next
      // call issues a fresh request instead of reusing the in-flight promise.
      await new Promise(resolve => setTimeout(resolve, 0))

      await ai.fetchAiStatus()
      expect(ai.isAiEnabled.value).toBe(false)

      // The failed fetch still initialized the singleton, so the next call
      // returns immediately without re-fetching.
      await ai.fetchAiStatus()

      expect(apiMock.get).toHaveBeenCalledTimes(2)
      expect(ai.isAiEnabled.value).toBe(false)
    })
  })
})
