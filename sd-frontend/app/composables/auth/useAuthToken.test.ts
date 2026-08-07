import { describe, it, expect, beforeEach, vi } from 'vitest'
import { nextTick } from 'vue'
import { useCookie } from '#app/composables/cookie'

type UseAuthToken = typeof import('~/composables/auth/useAuthToken')['default']

let useAuthToken: UseAuthToken

// The composable registers its cookie→state watch once via a module-level
// `_syncInitialized` flag. The setup.ts beforeEach clears the state/cookie
// stores but not that flag, so a watch registered in an earlier test would
// stay bound to stale refs. `vi.resetModules()` + a fresh dynamic import
// gives every test its own module instance with a clean flag.
beforeEach(async () => {
  vi.resetModules()
  const mod = await import('~/composables/auth/useAuthToken')
  useAuthToken = mod.default
})

describe('useAuthToken', () => {
  describe('setToken', () => {
    it('sets both access and refresh tokens in state', () => {
      const { setToken, getToken, getRefreshToken } = useAuthToken()

      setToken('access-123', 'refresh-456')

      expect(getToken()).toBe('access-123')
      expect(getRefreshToken()).toBe('refresh-456')
    })

    it('sets both tokens in cookies', () => {
      const { setToken } = useAuthToken()

      setToken('access-123', 'refresh-456')

      expect(useCookie<string | null>('auth-token').value).toBe('access-123')
      expect(useCookie<string | null>('refresh-token').value).toBe('refresh-456')
    })

    it('overwrites previously stored tokens when called again', () => {
      const { setToken, getToken, getRefreshToken } = useAuthToken()

      setToken('access-1', 'refresh-1')
      setToken('access-2', 'refresh-2')

      expect(getToken()).toBe('access-2')
      expect(getRefreshToken()).toBe('refresh-2')
      expect(useCookie<string | null>('auth-token').value).toBe('access-2')
      expect(useCookie<string | null>('refresh-token').value).toBe('refresh-2')
    })
  })

  describe('getToken', () => {
    it('returns null when no token has been set', () => {
      const { getToken } = useAuthToken()

      expect(getToken()).toBeNull()
    })

    it('returns the current access token after setToken', () => {
      const { setToken, getToken } = useAuthToken()

      setToken('access-123', 'refresh-456')

      expect(getToken()).toBe('access-123')
    })

    it('returns null after removeToken', () => {
      const { setToken, getToken, removeToken } = useAuthToken()

      setToken('access-123', 'refresh-456')
      removeToken()

      expect(getToken()).toBeNull()
    })
  })

  describe('getRefreshToken', () => {
    it('returns null when no token has been set', () => {
      const { getRefreshToken } = useAuthToken()

      expect(getRefreshToken()).toBeNull()
    })

    it('returns the current refresh token after setToken', () => {
      const { setToken, getRefreshToken } = useAuthToken()

      setToken('access-123', 'refresh-456')

      expect(getRefreshToken()).toBe('refresh-456')
    })

    it('returns null after removeToken', () => {
      const { setToken, getRefreshToken, removeToken } = useAuthToken()

      setToken('access-123', 'refresh-456')
      removeToken()

      expect(getRefreshToken()).toBeNull()
    })
  })

  describe('removeToken', () => {
    it('clears access and refresh tokens from state and cookies', () => {
      const { setToken, getToken, getRefreshToken, removeToken } = useAuthToken()

      setToken('access-123', 'refresh-456')
      removeToken()

      expect(getToken()).toBeNull()
      expect(getRefreshToken()).toBeNull()
      expect(useCookie<string | null>('auth-token').value).toBeNull()
      expect(useCookie<string | null>('refresh-token').value).toBeNull()
    })

    it('is safe to call when no token is set', () => {
      const { removeToken } = useAuthToken()

      expect(() => removeToken()).not.toThrow()
    })
  })

  describe('state ↔ cookie bridge', () => {
    it('keeps state and cookie in sync after setToken', () => {
      const { setToken, getToken, getRefreshToken } = useAuthToken()

      setToken('access-123', 'refresh-456')

      expect(getToken()).toBe('access-123')
      expect(getRefreshToken()).toBe('refresh-456')
      expect(useCookie<string | null>('auth-token').value).toBe('access-123')
      expect(useCookie<string | null>('refresh-token').value).toBe('refresh-456')
    })

    it('propagates external cookie changes back to state via the registered watch', async () => {
      const { getToken, getRefreshToken } = useAuthToken()
      const tokenCookie = useCookie<string | null>('auth-token')
      const refreshTokenCookie = useCookie<string | null>('refresh-token')

      tokenCookie.value = 'cookie-access'
      refreshTokenCookie.value = 'cookie-refresh'
      await nextTick()

      expect(getToken()).toBe('cookie-access')
      expect(getRefreshToken()).toBe('cookie-refresh')
    })
  })
})
