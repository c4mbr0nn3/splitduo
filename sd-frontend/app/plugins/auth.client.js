export default defineNuxtPlugin({
  name: 'auth-init',
  async setup() {
    const { initialize, user } = useAuth()
    const { syncFromUser } = useUserSettings()

    // Initialize auth state when the app starts
    await initialize()

    // Apply saved theme whenever user state changes (login, refresh, reload)
    watch(user, (u) => {
      if (u) syncFromUser(u)
    }, { immediate: true })

    // Redirect to / when user becomes null mid-session (e.g. failed token refresh)
    watch(user, (val) => {
      if (!val && useRoute().path !== '/') {
        navigateTo('/')
      }
    })
  },
})
