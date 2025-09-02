export function useAuth() {
  const api = useApi()
  const { setToken, removeToken, getToken, getRefreshToken } = useAuthToken()

  const userCookie = useCookie('auth-user', {
    default: () => null,
    secure: true,
    sameSite: 'strict',
    serializer: {
      read: (value) => {
        try {
          return value ? JSON.parse(value) : null
        }
        catch {
          return null
        }
      },
      write: value => JSON.stringify(value),
    },
  })

  const user = useState('user', () => userCookie.value)
  const isAuthenticated = computed(() => !!user.value)
  const isLoading = ref(false)

  // Login
  const login = async (credentials) => {
    isLoading.value = true
    try {
      const response = await api.post('/auth/login', credentials)

      if (response.success && response.data) {
        setToken(response.data.token, response.data.refreshToken)
        user.value = response.data.user
        userCookie.value = response.data.user
        return { success: true }
      }

      return { success: false, error: response.error?.message }
    }
    catch (error) {
      return {
        success: false,
        error: error.message || 'Login failed',
      }
    }
    finally {
      isLoading.value = false
    }
  }

  // Logout
  const logout = async () => {
    try {
      const refreshToken = getRefreshToken()
      if (refreshToken) {
        await api.post('/auth/revoke', { refreshToken })
      }
    }
    catch (error) {
      console.warn('Logout API call failed:', error)
    }
    finally {
      removeToken()
      user.value = null
      userCookie.value = null
      await navigateTo('/login')
    }
  }

  // Refresh token
  const refreshToken = async () => {
    try {
      const currentToken = getToken()
      const currentRefreshToken = getRefreshToken()

      if (!currentRefreshToken) return false

      const response = await api.post('/auth/refresh', {
        token: currentToken, // expired token
        refreshToken: currentRefreshToken,
      })

      if (response.success && response.data) {
        setToken(response.data.token, response.data.refreshToken)
        user.value = response.data.user
        userCookie.value = response.data.user
        return true
      }

      return false
    }
    catch (error) {
      console.error('Token refresh failed:', error)
      await logout()
      return false
    }
  }

  // Initialize auth state
  const initialize = async () => {
    const token = getToken()
    if (!token) return

    try {
      const response = await api.get('/users/me')
      if (response.success && response.data) {
        user.value = response.data
        userCookie.value = response.data
      }
    }
    catch (error) {
      if (error.statusCode === 401) {
        // Try to refresh token
        const refreshed = await refreshToken()
        if (refreshed) {
          await initialize() // Retry with new token
        }
      }
    }
  }

  return {
    user: readonly(user),
    isAuthenticated,
    isLoading: readonly(isLoading),
    login,
    logout,
    refreshToken,
    initialize,
  }
}
