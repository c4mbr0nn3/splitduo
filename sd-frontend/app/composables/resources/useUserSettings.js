import { useDebounceFn } from '@vueuse/core'

export default function useUserSettings() {
  const api = useApi()
  const colorMode = useColorMode()
  const nuxtApp = useNuxtApp()
  const notifications = useNotifications()

  const settings = useState('user-settings', () => ({
    theme: 'auto',
    uiLanguage: 'en',
  }))

  const applyTheme = (theme) => {
    colorMode.preference = theme === 'auto' ? 'system' : theme
  }

  const syncFromUser = (user) => {
    if (user?.settings) {
      settings.value = user.settings
      applyTheme(user.settings.theme)
      if (user.settings.uiLanguage) {
        nuxtApp.$i18n?.setLocale(user.settings.uiLanguage)
      }
    }
  }

  const update = useDebounceFn(async (patch) => {
    // Apply locale immediately if uiLanguage is being changed
    if (patch.uiLanguage) {
      nuxtApp.$i18n?.setLocale(patch.uiLanguage)
    }
    const res = await api.put('/users/me/settings', patch)
    if (res.success && res.data) {
      settings.value = res.data
      applyTheme(res.data.theme)
    }
    else {
      // Revert optimistic colorMode and locale to last-known-good server value
      applyTheme(settings.value.theme)
      if (patch.uiLanguage) {
        nuxtApp.$i18n?.setLocale(settings.value.uiLanguage)
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
