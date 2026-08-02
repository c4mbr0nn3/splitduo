import type { User, AuthResponse, LoginRequest } from '~/types/domain'

export interface LoginResult {
  success: boolean
  requiresTwoFactor: boolean
  error?: string
}

const pendingChallengeToken = ref<string | null>(null)

export default function useAuth() {
  const api = useApi()
  const { setToken, removeToken, getToken, getRefreshToken } = useAuthToken()

  const userCookie = useCookie<User | null>('auth-user', {
    default: () => null,
    secure: true,
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7,
  })

  const user = useState<User | null>('user', () => userCookie.value)
  const isAuthenticated = computed(() => !!user.value)
  const isGlobalAdmin = computed(() => user.value?.globalRoleId == 2)
  const isLoading = ref(false)

  // Login
  const login = async (credentials: LoginRequest): Promise<LoginResult> => {
    isLoading.value = true
    try {
      const response = await api.post<AuthResponse>('/auth/login', credentials)

      if (response.success && response.data) {
        if (response.data.requiresTwoFactor) {
          pendingChallengeToken.value = response.data.twoFactorChallengeToken ?? null
          return { success: true, requiresTwoFactor: true }
        }
        setToken(response.data.token, response.data.refreshToken)
        user.value = response.data.user as User ?? null
        userCookie.value = response.data.user as User ?? null
        return { success: true, requiresTwoFactor: false }
      }

      return { success: false, requiresTwoFactor: false, error: response.error?.message ?? undefined }
    }
    catch (error: unknown) {
      return {
        success: false,
        requiresTwoFactor: false,
        error: error instanceof Error ? error.message : 'Login failed',
      }
    }
    finally {
      isLoading.value = false
    }
  }

  // Complete login after 2FA verification
  const completeTwoFactorLogin = (authData: AuthResponse): void => {
    setToken(authData.token, authData.refreshToken)
    user.value = authData.user as User ?? null
    userCookie.value = authData.user as User ?? null
    pendingChallengeToken.value = null
  }

  // Logout
  const logout = async (): Promise<void> => {
    try {
      const refreshToken = getRefreshToken()
      if (refreshToken) {
        await api.post('/auth/revoke', { refreshToken })
      }
    }
    catch (error: unknown) {
      console.warn('Logout API call failed:', error)
    }
    finally {
      removeToken()
      user.value = null
      userCookie.value = null
      await navigateTo('/')
    }
  }

  // Refresh token
  const refreshToken = async (): Promise<boolean> => {
    try {
      const currentToken = getToken()
      const currentRefreshToken = getRefreshToken()

      if (!currentRefreshToken) return false

      const response = await api.post<AuthResponse>('/auth/refresh', {
        token: currentToken, // expired token
        refreshToken: currentRefreshToken,
      })

      if (response.success && response.data) {
        setToken(response.data.token, response.data.refreshToken)
        user.value = response.data.user as User ?? null
        userCookie.value = response.data.user as User ?? null
        return true
      }

      return false
    }
    catch (error: unknown) {
      console.error('Token refresh failed:', error)
      removeToken()
      user.value = null
      userCookie.value = null
      return false
    }
  }

  // Initialize auth state
  const initialize = async (isRetry = false): Promise<void> => {
    const token = getToken()

    if (!token) {
      // Access token gone (expired cookie or first cold start after long background)
      // Try refresh token before giving up
      if (getRefreshToken()) {
        await refreshToken()
      }
      return
    }

    try {
      const response = await api.get<User>('/users/me')
      if (response.success && response.data) {
        user.value = response.data as User
        userCookie.value = response.data as User
      }
    }
    catch (error: unknown) {
      if (
        error
        && typeof error === 'object'
        && 'statusCode' in error
        && (error as { statusCode?: number }).statusCode === 401
        && !isRetry
      ) {
        // Access token expired mid-session — try refresh once
        const refreshed = await refreshToken()
        if (refreshed) {
          await initialize(true)
        }
      }
      else if (
        !(
          error
          && typeof error === 'object'
          && 'statusCode' in error
          && (error as { statusCode?: number }).statusCode === 401
        )
      ) {
        console.error('Failed to initialize auth state:', error)
      }
    }
  }

  return {
    user: readonly(user),
    isAuthenticated,
    isGlobalAdmin: readonly(isGlobalAdmin),
    isLoading: readonly(isLoading),
    pendingChallengeToken: readonly(pendingChallengeToken),
    login,
    logout,
    refreshToken,
    initialize,
    completeTwoFactorLogin,
  }
}
