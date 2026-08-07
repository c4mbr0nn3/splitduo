import { describe, it, expect, vi, beforeAll, beforeEach, afterEach } from 'vitest'

import { apiMock } from '~/composables/api/base.mock'
import type { Category } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useCategories.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n'; mock the module so `t` is a
// controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const categories: Category[] = [
  { id: 1, name: 'Food' },
  { id: 2, name: 'Transport' },
]

// useCategories is a singleton: its module-level refs (globalCategories,
// globalIsLoading, globalIsInitialized) and fetchPromise persist across tests.
// Reset the module registry in beforeEach and import a fresh module instance so
// every test starts from the uninitialized state.
//
// The first dynamic import of the module graph pays a one-off transform cost
// that can exceed the default 5000ms test timeout, so warm it up in beforeAll
// (the registry is reset in beforeEach, so tests still get fresh instances).
describe('useCategories', () => {
  beforeAll(async () => {
    await import('./useCategories')
  })

  beforeEach(() => {
    vi.clearAllMocks()
    vi.resetModules()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchCategories', () => {
    it('fetches from the API and stores categories on first call', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: categories })
      const { default: useCategories } = await import('./useCategories')
      const uc = useCategories()

      await uc.fetchCategories()

      expect(apiMock.get).toHaveBeenCalledWith('/categories')
      expect(uc.categories.value).toEqual(categories)
      expect(uc.isLoading.value).toBe(false)
    })

    it('returns immediately without calling the API once initialized', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: categories })
      const { default: useCategories } = await import('./useCategories')
      const uc = useCategories()
      await uc.fetchCategories()
      expect(apiMock.get).toHaveBeenCalledTimes(1)

      await uc.fetchCategories()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(uc.categories.value).toEqual(categories)
    })

    it('deduplicates concurrent calls by sharing the same fetch promise', async () => {
      let resolveFetch: (value: unknown) => void = () => {}
      apiMock.get.mockImplementation(() => new Promise((resolve) => {
        resolveFetch = resolve
      }))
      const { default: useCategories } = await import('./useCategories')
      const uc = useCategories()

      const first = uc.fetchCategories()
      const second = uc.fetchCategories()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(uc.isLoading.value).toBe(true)

      resolveFetch({ success: true, data: categories })
      await Promise.all([first, second])

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(uc.categories.value).toEqual(categories)
      expect(uc.isLoading.value).toBe(false)
    })

    it('shows an error toast, re-throws, and resets loading state when the API call fails', async () => {
      // The auto-init on first use fires a fetch whose promise is discarded by
      // the composable; give it a non-throwing response so that discarded
      // promise resolves instead of surfacing as an unhandled rejection.
      apiMock.get.mockResolvedValueOnce({ success: false, data: null })
      apiMock.get.mockRejectedValueOnce(new Error('Network down'))
      const { default: useCategories } = await import('./useCategories')
      const uc = useCategories()

      // Let the auto-init fetch settle so fetchPromise is cleared and the next
      // call issues a fresh request instead of reusing the in-flight promise.
      await new Promise(resolve => setTimeout(resolve, 0))

      await expect(uc.fetchCategories()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.categories.loadFailed')
      expect(uc.isLoading.value).toBe(false)
    })

    it('allows a retry after a failed fetch', async () => {
      // The auto-init on first use fires a fetch whose promise is discarded by
      // the composable; give it a non-throwing response so that discarded
      // promise resolves instead of surfacing as an unhandled rejection.
      apiMock.get.mockResolvedValueOnce({ success: false, data: null })
      apiMock.get.mockRejectedValueOnce(new Error('Network down'))
      apiMock.get.mockResolvedValueOnce({ success: true, data: categories })
      const { default: useCategories } = await import('./useCategories')
      const uc = useCategories()

      // Let the auto-init fetch settle so fetchPromise is cleared and the next
      // call issues a fresh request instead of reusing the in-flight promise.
      await new Promise(resolve => setTimeout(resolve, 0))

      await expect(uc.fetchCategories()).rejects.toThrow('Network down')
      await uc.fetchCategories()

      expect(apiMock.get).toHaveBeenCalledTimes(3)
      expect(uc.categories.value).toEqual(categories)
    })
  })

  describe('getCategoryName', () => {
    it('returns the category name for a valid id', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: categories })
      const { default: useCategories } = await import('./useCategories')
      const uc = useCategories()
      await uc.fetchCategories()

      expect(uc.getCategoryName(1)).toBe('Food')
      expect(uc.getCategoryName(2)).toBe('Transport')
    })

    it('returns \'Unknown\' for an invalid id', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: categories })
      const { default: useCategories } = await import('./useCategories')
      const uc = useCategories()
      await uc.fetchCategories()

      expect(uc.getCategoryName(999)).toBe('Unknown')
    })
  })

  describe('auto-initialization', () => {
    it('triggers a fetch on first use when not initialized', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: categories })
      const { default: useCategories } = await import('./useCategories')

      const uc = useCategories()

      expect(apiMock.get).toHaveBeenCalledWith('/categories')
      await uc.fetchCategories()
      expect(uc.categories.value).toEqual(categories)
    })

    it('does not auto-fetch again on subsequent uses once initialized', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: categories })
      const { default: useCategories } = await import('./useCategories')
      const first = useCategories()
      await first.fetchCategories()
      expect(apiMock.get).toHaveBeenCalledTimes(1)

      const second = useCategories()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(second.categories.value).toEqual(categories)
    })

    it('does not auto-fetch when a fetch is already in flight', async () => {
      let resolveFetch: (value: unknown) => void = () => {}
      apiMock.get.mockImplementation(() => new Promise((resolve) => {
        resolveFetch = resolve
      }))
      const { default: useCategories } = await import('./useCategories')

      const first = useCategories()
      expect(apiMock.get).toHaveBeenCalledTimes(1)

      const second = useCategories()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      resolveFetch({ success: true, data: categories })
      await first.fetchCategories()
      expect(second.categories.value).toEqual(categories)
    })
  })
})
