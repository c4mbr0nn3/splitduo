import { useDebounceFn } from '@vueuse/core'

export default function useUserSettings() {
  const api = useApi()
  const colorMode = useColorMode()
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
    }
  }

  const update = useDebounceFn(async (patch) => {
    const res = await api.put('/users/me/settings', patch)
    if (res.success && res.data) {
      settings.value = res.data
      applyTheme(res.data.theme)
    }
    else {
      // Revert optimistic colorMode to last-known-good server value
      applyTheme(settings.value.theme)
      notifications.showError(res.error?.message || 'Failed to save settings')
    }
    return res
  }, 150)

  return {
    settings: readonly(settings),
    syncFromUser,
    update,
  }
}
