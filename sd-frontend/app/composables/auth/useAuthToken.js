export default function useAuthToken() {
  const tokenCookie = useCookie('auth-token', {
    default: () => null,
    secure: true,
    sameSite: 'strict',
    maxAge: 60 * 60 * 24 * 7, // 7 days (JWT expiry enforced by backend)
  })

  const refreshTokenCookie = useCookie('refresh-token', {
    default: () => null,
    secure: true,
    sameSite: 'strict',
    maxAge: 60 * 60 * 24 * 7, // 7 days (matches backend refresh token expiry)
  })

  const setToken = (token, refreshToken) => {
    tokenCookie.value = token
    refreshTokenCookie.value = refreshToken
  }

  const getToken = () => {
    return tokenCookie.value
  }

  const getRefreshToken = () => {
    return refreshTokenCookie.value
  }

  const removeToken = () => {
    tokenCookie.value = null
    refreshTokenCookie.value = null
  }

  return {
    setToken,
    getToken,
    getRefreshToken,
    removeToken,
  }
}
