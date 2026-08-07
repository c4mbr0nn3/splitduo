import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import useAuth from './useAuth'
import { apiMock } from '~/composables/api/base.mock'
import { useCookie } from '#app/composables/cookie'
import { navigateTo } from '#app/composables/router'
import type { AuthResponse, User } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const authTokenMock = vi.hoisted(() => ({
  setToken: vi.fn(),
  getToken: vi.fn(),
  getRefreshToken: vi.fn(),
  removeToken: vi.fn(),
}))

// useApi / useAuthToken are auto-imported inside useAuth.ts; mock the composable
// modules so every API call and token transition is controlled from the test.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('~/composables/auth/useAuthToken', () => ({ default: () => authTokenMock }))

const user: User = {
  id: 'user-1',
  email: 'alice@example.com',
  firstName: 'Alice',
  globalRoleId: 1,
  createdAt: 0,
  updatedAt: 0,
  twoFactorEnabled: false,
  settings: { theme: 'light', uiLanguage: 'en' },
}

const authResponse: AuthResponse = {
  token: 'access-123',
  refreshToken: 'refresh-456',
  expiresAt: 9999999999,
  requiresTwoFactor: false,
  user,
}

const credentials = { email: 'alice@example.com', password: 'secret' }

describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    authTokenMock.getToken.mockReturnValue(null)
    authTokenMock.getRefreshToken.mockReturnValue(null)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('login', () => {
    it('sets the token and user on a successful login without 2FA', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: authResponse })
      const auth = useAuth()

      const result = await auth.login(credentials)

      expect(result).toEqual({ success: true, requiresTwoFactor: false })
      expect(apiMock.post).toHaveBeenCalledWith('/auth/login', credentials)
      expect(authTokenMock.setToken).toHaveBeenCalledWith('access-123', 'refresh-456')
      expect(auth.user.value).toEqual(user)
      expect(useCookie<User | null>('auth-user').value).toEqual(user)
    })

    it('stores the challenge token and skips token setup when 2FA is required', async () => {
      apiMock.post.mockResolvedValue({
        success: true,
        data: { ...authResponse, requiresTwoFactor: true, twoFactorChallengeToken: 'challenge-1' },
      })
      const auth = useAuth()

      const result = await auth.login(credentials)

      expect(result).toEqual({ success: true, requiresTwoFactor: true })
      expect(auth.pendingChallengeToken.value).toBe('challenge-1')
      expect(authTokenMock.setToken).not.toHaveBeenCalled()
      expect(auth.user.value).toBeNull()
    })

    it('returns the API error message when the response is unsuccessful', async () => {
      apiMock.post.mockResolvedValue({
        success: false,
        data: null,
        error: { code: 'INVALID_CREDENTIALS', message: 'Invalid credentials' },
      })
      const auth = useAuth()

      const result = await auth.login(credentials)

      expect(result).toEqual({ success: false, requiresTwoFactor: false, error: 'Invalid credentials' })
    })

    it('returns a failure result when the API call throws', async () => {
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const auth = useAuth()

      const result = await auth.login(credentials)

      expect(result).toEqual({ success: false, requiresTwoFactor: false, error: 'Network down' })
    })

    it('sets isLoading during the request and clears it afterwards', async () => {
      let resolveLogin: (value: unknown) => void = () => {}
      apiMock.post.mockImplementation(() => new Promise((resolve) => {
        resolveLogin = resolve
      }))
      const auth = useAuth()

      const pending = auth.login(credentials)
      expect(auth.isLoading.value).toBe(true)

      resolveLogin({ success: true, data: authResponse })
      await pending

      expect(auth.isLoading.value).toBe(false)
    })
  })

  describe('completeTwoFactorLogin', () => {
    it('sets the token and user, and clears the pending challenge token', async () => {
      apiMock.post.mockResolvedValue({
        success: true,
        data: { ...authResponse, requiresTwoFactor: true, twoFactorChallengeToken: 'challenge-1' },
      })
      const auth = useAuth()
      await auth.login(credentials)
      expect(auth.pendingChallengeToken.value).toBe('challenge-1')

      auth.completeTwoFactorLogin(authResponse)

      expect(authTokenMock.setToken).toHaveBeenCalledWith('access-123', 'refresh-456')
      expect(auth.user.value).toEqual(user)
      expect(useCookie<User | null>('auth-user').value).toEqual(user)
      expect(auth.pendingChallengeToken.value).toBeNull()
    })
  })

  describe('logout', () => {
    it('revokes the refresh token when one exists', async () => {
      authTokenMock.getRefreshToken.mockReturnValue('refresh-456')
      const auth = useAuth()

      await auth.logout()

      expect(apiMock.post).toHaveBeenCalledWith('/auth/revoke', { refreshToken: 'refresh-456' })
    })

    it('skips the revoke call when no refresh token exists', async () => {
      const auth = useAuth()

      await auth.logout()

      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('clears the token and user, and navigates to the home page', async () => {
      authTokenMock.getRefreshToken.mockReturnValue('refresh-456')
      const auth = useAuth()
      auth.completeTwoFactorLogin(authResponse)

      await auth.logout()

      expect(authTokenMock.removeToken).toHaveBeenCalled()
      expect(auth.user.value).toBeNull()
      expect(useCookie<User | null>('auth-user').value).toBeNull()
      expect(navigateTo).toHaveBeenCalledWith('/')
    })

    it('catches revoke failures and warns instead of throwing', async () => {
      authTokenMock.getRefreshToken.mockReturnValue('refresh-456')
      apiMock.post.mockRejectedValue(new Error('Revoke failed'))
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})
      const auth = useAuth()

      await expect(auth.logout()).resolves.toBeUndefined()

      expect(warnSpy).toHaveBeenCalledWith('Logout API call failed:', expect.any(Error))
      warnSpy.mockRestore()
    })
  })

  describe('refreshToken', () => {
    it('updates the token and user on success', async () => {
      authTokenMock.getToken.mockReturnValue('expired-token')
      authTokenMock.getRefreshToken.mockReturnValue('refresh-456')
      apiMock.post.mockResolvedValue({ success: true, data: authResponse })
      const auth = useAuth()

      const result = await auth.refreshToken()

      expect(result).toBe(true)
      expect(apiMock.post).toHaveBeenCalledWith('/auth/refresh', { token: 'expired-token', refreshToken: 'refresh-456' })
      expect(authTokenMock.setToken).toHaveBeenCalledWith('access-123', 'refresh-456')
      expect(auth.user.value).toEqual(user)
      expect(useCookie<User | null>('auth-user').value).toEqual(user)
    })

    it('returns false without calling the API when no refresh token exists', async () => {
      const auth = useAuth()

      const result = await auth.refreshToken()

      expect(result).toBe(false)
      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('clears the session and returns false when the API call throws', async () => {
      authTokenMock.getRefreshToken.mockReturnValue('refresh-456')
      apiMock.post.mockRejectedValue(new Error('Refresh failed'))
      const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
      const auth = useAuth()
      auth.completeTwoFactorLogin(authResponse)

      const result = await auth.refreshToken()

      expect(result).toBe(false)
      expect(authTokenMock.removeToken).toHaveBeenCalled()
      expect(auth.user.value).toBeNull()
      expect(useCookie<User | null>('auth-user').value).toBeNull()
      expect(errorSpy).toHaveBeenCalledWith('Token refresh failed:', expect.any(Error))
      errorSpy.mockRestore()
    })
  })

  describe('initialize', () => {
    it('does not call the API when no token or refresh token exists', async () => {
      const auth = useAuth()

      await auth.initialize()

      expect(apiMock.get).not.toHaveBeenCalled()
      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('refreshes the token when only a refresh token exists', async () => {
      authTokenMock.getRefreshToken.mockReturnValue('refresh-456')
      apiMock.post.mockResolvedValue({ success: true, data: authResponse })
      const auth = useAuth()

      await auth.initialize()

      expect(apiMock.post).toHaveBeenCalledWith('/auth/refresh', { token: null, refreshToken: 'refresh-456' })
      expect(authTokenMock.setToken).toHaveBeenCalledWith('access-123', 'refresh-456')
    })

    it('restores the user from /users/me when a token exists', async () => {
      authTokenMock.getToken.mockReturnValue('access-123')
      apiMock.get.mockResolvedValue({ success: true, data: user })
      const auth = useAuth()

      await auth.initialize()

      expect(apiMock.get).toHaveBeenCalledWith('/users/me')
      expect(auth.user.value).toEqual(user)
      expect(useCookie<User | null>('auth-user').value).toEqual(user)
    })

    it('refreshes once and retries after a 401 when not already a retry', async () => {
      authTokenMock.getToken.mockReturnValue('access-123')
      authTokenMock.getRefreshToken.mockReturnValue('refresh-456')
      apiMock.get.mockRejectedValueOnce({ statusCode: 401, statusMessage: 'Unauthorized' })
      apiMock.get.mockResolvedValueOnce({ success: true, data: user })
      apiMock.post.mockResolvedValue({ success: true, data: authResponse })
      const auth = useAuth()

      await auth.initialize()

      expect(apiMock.post).toHaveBeenCalledWith('/auth/refresh', { token: 'access-123', refreshToken: 'refresh-456' })
      expect(apiMock.get).toHaveBeenCalledTimes(2)
      expect(auth.user.value).toEqual(user)
    })

    it('does not retry when the 401 happens during a retry', async () => {
      authTokenMock.getToken.mockReturnValue('access-123')
      apiMock.get.mockRejectedValue({ statusCode: 401, statusMessage: 'Unauthorized' })
      const auth = useAuth()

      await auth.initialize(true)

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('logs the error and does not retry on a non-401 failure', async () => {
      authTokenMock.getToken.mockReturnValue('access-123')
      apiMock.get.mockRejectedValue({ statusCode: 500, statusMessage: 'Server error' })
      const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
      const auth = useAuth()

      await auth.initialize()

      expect(apiMock.get).toHaveBeenCalledTimes(1)
      expect(apiMock.post).not.toHaveBeenCalled()
      expect(errorSpy).toHaveBeenCalledWith('Failed to initialize auth state:', expect.any(Object))
      errorSpy.mockRestore()
    })
  })

  describe('computed state', () => {
    it('isAuthenticated reflects whether a user is set', () => {
      const auth = useAuth()

      expect(auth.isAuthenticated.value).toBe(false)

      auth.completeTwoFactorLogin(authResponse)

      expect(auth.isAuthenticated.value).toBe(true)
    })

    it('isGlobalAdmin is true only for users with globalRoleId 2', () => {
      const auth = useAuth()

      auth.completeTwoFactorLogin({ ...authResponse, user: { ...user, globalRoleId: 2 } })
      expect(auth.isGlobalAdmin.value).toBe(true)

      auth.completeTwoFactorLogin({ ...authResponse, user: { ...user, globalRoleId: 1 } })
      expect(auth.isGlobalAdmin.value).toBe(false)
    })
  })
})
