import { useAuth } from '~/composables/auth/useAuth'

export default defineNuxtPlugin({
  name: 'auth-init',
  async setup() {
    const { initialize } = useAuth()

    // Initialize auth state when the app starts
    await initialize()
  },
})
