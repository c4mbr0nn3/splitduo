import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'

import useUserSettings from './useUserSettings'
import { apiMock } from '~/composables/api/base.mock'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))
const colorModeMock = vi.hoisted(() => ({
  preference: 'light',
  value: 'light',
  unknown: false,
  forced: false,
}))
const i18nMock = vi.hoisted(() => ({
  locale: 'en',
  setLocale: vi.fn(),
  t: vi.fn((key: string) => key),
}))

// useApi / useNotifications are auto-imported inside useUserSettings.ts; mock
// the composable modules so every API call and toast is controlled from the
// test. useColorMode is auto-imported from @nuxtjs/color-mode; mockNuxtImport
// resolves the path from Nuxt's live unimport context at build time, so it
// survives pnpm store hash changes on dependency bumps.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))
mockNuxtImport('useColorMode', () => () => colorModeMock)
vi.mock('#app/nuxt', async (importOriginal) => {
  const actual = await importOriginal<typeof import('#app/nuxt')>()
  return {
    ...actual,
    useNuxtApp: () => ({ $config: { public: {} }, $i18n: i18nMock }),
    useRuntimeConfig: () => ({ public: { apiBaseUrl: '/api/v1' } }),
  }
})

// update() is debounced with a 150ms delay (useDebounceFn from @vueuse/core);
// fake timers let the tests advance past the debounce deterministically.
describe('useUserSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.useFakeTimers()
    colorModeMock.preference = 'light'
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  describe('syncFromUser', () => {
    it('applies the user settings, theme, and locale', () => {
      const settings = useUserSettings()

      settings.syncFromUser({ settings: { theme: 'dark', uiLanguage: 'it' } })

      expect(settings.settings.value).toEqual({ theme: 'dark', uiLanguage: 'it' })
      expect(colorModeMock.preference).toBe('dark')
      expect(i18nMock.setLocale).toHaveBeenCalledWith('it')
    })

    it('maps theme \'auto\' to the system color-mode preference', () => {
      const settings = useUserSettings()

      settings.syncFromUser({ settings: { theme: 'auto', uiLanguage: 'en' } })

      expect(colorModeMock.preference).toBe('system')
    })

    it('defaults missing fields and skips the locale switch when uiLanguage is absent', () => {
      const settings = useUserSettings()

      settings.syncFromUser({ settings: { theme: 'dark' } })

      expect(settings.settings.value).toEqual({ theme: 'dark', uiLanguage: 'en' })
      expect(colorModeMock.preference).toBe('dark')
      expect(i18nMock.setLocale).not.toHaveBeenCalled()
    })

    it('is a no-op when the user has no settings', () => {
      const settings = useUserSettings()

      settings.syncFromUser(null)
      settings.syncFromUser({})

      expect(settings.settings.value).toEqual({ theme: 'auto', uiLanguage: 'en' })
      expect(colorModeMock.preference).toBe('light')
      expect(i18nMock.setLocale).not.toHaveBeenCalled()
    })
  })

  describe('update', () => {
    it('PUTs the patch after the debounce and applies the returned settings', async () => {
      apiMock.put.mockResolvedValue({
        success: true,
        data: { settings: { theme: 'dark', uiLanguage: 'en' } },
      })
      const settings = useUserSettings()

      const pending = settings.update({ theme: 'dark' })
      expect(apiMock.put).not.toHaveBeenCalled()

      await vi.advanceTimersByTimeAsync(150)
      await pending

      expect(apiMock.put).toHaveBeenCalledWith('/users/me/settings', { theme: 'dark' })
      expect(settings.settings.value).toEqual({ theme: 'dark', uiLanguage: 'en' })
      expect(colorModeMock.preference).toBe('dark')
    })

    it('applies the locale immediately when uiLanguage changes', async () => {
      apiMock.put.mockResolvedValue({
        success: true,
        data: { settings: { theme: 'auto', uiLanguage: 'it' } },
      })
      const settings = useUserSettings()

      const pending = settings.update({ uiLanguage: 'it' })
      await vi.advanceTimersByTimeAsync(150)
      await pending

      expect(i18nMock.setLocale).toHaveBeenCalledWith('it')
      expect(apiMock.put).toHaveBeenCalledWith('/users/me/settings', { uiLanguage: 'it' })
      expect(settings.settings.value).toEqual({ theme: 'auto', uiLanguage: 'it' })
    })

    it('reverts the optimistic theme and locale and shows an error when the response is unsuccessful', async () => {
      apiMock.put.mockResolvedValue({ success: false, data: null })
      const settings = useUserSettings()
      settings.syncFromUser({ settings: { theme: 'light', uiLanguage: 'en' } })

      const pending = settings.update({ theme: 'dark', uiLanguage: 'it' })
      await vi.advanceTimersByTimeAsync(150)
      await pending

      // Reverted to the last-known-good server values
      expect(colorModeMock.preference).toBe('light')
      expect(i18nMock.setLocale).toHaveBeenLastCalledWith('en')
      expect(settings.settings.value).toEqual({ theme: 'light', uiLanguage: 'en' })
      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.settings.saveFailed')
    })

    it('rejects when the API call throws', async () => {
      apiMock.put.mockRejectedValue(new Error('Network down'))
      const settings = useUserSettings()

      const pending = settings.update({ theme: 'dark' })
      // Attach the rejection handler before the debounce fires so the rejected
      // promise is not flagged as unhandled while timers advance.
      const rejection = expect(pending).rejects.toThrow('Network down')
      await vi.advanceTimersByTimeAsync(150)

      await rejection
    })
  })
})
