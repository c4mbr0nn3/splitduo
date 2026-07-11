export default defineNuxtPlugin({
  name: 'auth-init',
  async setup() {
    const { initialize, user } = useAuth()

    // Initialize auth state when the app starts
    await initialize()

    // Redirect to / when user becomes null mid-session (e.g. failed token refresh)
    watch(user, (val) => {
      if (!val && useRoute().path !== '/') {
        navigateTo('/')
      }
    })
  },
})
