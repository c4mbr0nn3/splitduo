import type { UserSettings, UpdateUserSettingsRequest, UpdateUserSettingsResponse, ApiEnvelope } from '~/types/domain'
import { useDebounceFn } from '@vueuse/core'

type Locale = 'en' | 'it'

export default function useUserSettings() {
  const api = useApi()
  const colorMode = useColorMode()
  const nuxtApp = useNuxtApp()
  const notifications = useNotifications()

  const settings = useState<UserSettings>('user-settings', () => ({
    theme: 'auto',
    uiLanguage: 'en',
  }))

  const applyTheme = (theme: string) => {
    colorMode.preference = theme === 'auto' ? 'system' : theme
  }

  // Accepts the readonly User shape from useAuth(). The settings fields arrive
  // structurally optional after Readonly<WithRequired<>> distribution, so we
  // narrow with defaults before assigning to the required-fields UserSettings ref.
  const syncFromUser = (user: { settings?: { theme?: string, uiLanguage?: string } | undefined } | null) => {
    if (user?.settings) {
      const s = user.settings
      settings.value = {
        theme: s.theme ?? 'auto',
        uiLanguage: s.uiLanguage ?? 'en',
      }
      applyTheme(s.theme ?? 'auto')
      if (s.uiLanguage) {
        nuxtApp.$i18n?.setLocale(s.uiLanguage as Locale)
      }
    }
  }

  const update = useDebounceFn(async (patch: UpdateUserSettingsRequest): Promise<ApiEnvelope<UpdateUserSettingsResponse>> => {
    // Apply locale immediately if uiLanguage is being changed
    if (patch.uiLanguage) {
      nuxtApp.$i18n?.setLocale(patch.uiLanguage as Locale)
    }
    const res = await api.put<UpdateUserSettingsResponse>('/users/me/settings', patch)
    if (res.success && res.data) {
      settings.value = res.data.settings as UserSettings
      applyTheme((res.data.settings as UserSettings).theme)
    }
    else {
      // Revert optimistic colorMode and locale to last-known-good server value
      applyTheme(settings.value.theme)
      if (patch.uiLanguage) {
        nuxtApp.$i18n?.setLocale(settings.value.uiLanguage as Locale)
      }
      notifications.showError(nuxtApp.$i18n?.t('toasts.settings.saveFailed') || 'Failed to save settings')
    }
    return res
  }, 150)

  return {
    settings: readonly(settings),
    syncFromUser,
    update,
  }
}
