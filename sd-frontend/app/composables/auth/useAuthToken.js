// useState is shared across all callers (keyed by name) and reads are synchronous.
// useCookie refs with the same key only sync via async BroadcastChannel/CookieStore
// events, which fire after the refresh+retry sequence reads the token — so the retry
// would send the stale (expired) token and surface a spurious error toast. useState
// gives us a synchronous shared source of truth; useCookie persists to the browser.
let _syncInitialized = false

export default function useAuthToken() {
  const tokenCookie = useCookie('auth-token', {
    default: () => null,
    secure: true,
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7, // 7 days (JWT expiry enforced by backend)
  })

  const refreshTokenCookie = useCookie('refresh-token', {
    default: () => null,
    secure: true,
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7, // 7 days (matches backend refresh token expiry)
  })

  const token = useState('auth-token', () => tokenCookie.value)
  const refreshToken = useState('refresh-token', () => refreshTokenCookie.value)

  // Propagate external cookie changes (other tabs, server-set cookies) back to state.
  // Registered once; first call happens in the auth plugin (app-lifetime scope).
  if (!_syncInitialized) {
    _syncInitialized = true
    watch(tokenCookie, (v) => {
      token.value = v
    })
    watch(refreshTokenCookie, (v) => {
      refreshToken.value = v
    })
  }

  const setToken = (newToken, newRefreshToken) => {
    tokenCookie.value = newToken
    refreshTokenCookie.value = newRefreshToken
    token.value = newToken
    refreshToken.value = newRefreshToken
  }

  const getToken = () => token.value

  const getRefreshToken = () => refreshToken.value

  const removeToken = () => {
    tokenCookie.value = null
    refreshTokenCookie.value = null
    token.value = null
    refreshToken.value = null
  }

  return {
    setToken,
    getToken,
    getRefreshToken,
    removeToken,
  }
}
