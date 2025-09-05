export default function useAuthToken() {
  const tokenCookie = useCookie('auth-token', {
    default: () => null,
    secure: true,
    sameSite: 'strict',
  })

  const refreshTokenCookie = useCookie('refresh-token', {
    default: () => null,
    secure: true,
    sameSite: 'strict',
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
