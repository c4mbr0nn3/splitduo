// Shared across all useApi() instances — deduplicates concurrent refresh attempts
let _refreshPromise = null

export default function useApi() {
  const config = useRuntimeConfig()
  const { getToken } = useAuthToken()
  const nuxtApp = useNuxtApp()

  const apiConfig = {
    baseURL: import.meta.dev ? 'http://localhost:8080/api/v1' : config.public.apiBaseUrl,
  }

  // Create authenticated request headers
  const getAuthHeaders = () => {
    const token = getToken()
    return token
      ? { Authorization: `Bearer ${token}` }
      : {}
  }

  const fetchOnce = (endpoint, options) => {
    // Resolve current locale for Accept-Language header via $i18n (works in plugin + component context)
    const locale = nuxtApp.$i18n?.locale?.value || 'en'
    return $fetch.raw(`${apiConfig.baseURL}${endpoint}`, {
      headers: {
        // 'Content-Type': 'application/json',
        'Accept-Language': locale,
        ...getAuthHeaders(),
        ...(options.headers || {}),
      },
      ...options,
    })
  }

  // Base request function with error handling — returns raw response
  const requestRaw = async (endpoint, options = {}) => {
    try {
      return await fetchOnce(endpoint, options)
    }
    catch (error) {
      // On 401: attempt one token refresh then retry.
      // Skip /auth/* endpoints to avoid infinite loops.
      if (error.status === 401 && !endpoint.startsWith('/auth/')) {
        if (!_refreshPromise) {
          const { refreshToken } = useAuth()
          _refreshPromise = refreshToken().finally(() => {
            _refreshPromise = null
          })
        }
        const refreshed = await _refreshPromise
        if (refreshed) {
          try {
            return await fetchOnce(endpoint, options)
          }
          catch (retryError) {
            throw createError({
              statusCode: retryError.status || 500,
              statusMessage: retryError.message || 'API Error',
            })
          }
        }
      }
      throw createError({
        statusCode: error.status || 500,
        statusMessage: error.message || 'API Error',
      })
    }
  }

  // Thin wrapper that returns parsed body only
  const request = async (endpoint, options = {}) => {
    return (await requestRaw(endpoint, options))._data
  }

  return {
    get: (endpoint, params) =>
      request(endpoint, { method: 'GET', params }),

    post: (endpoint, body) =>
      request(endpoint, { method: 'POST', body }),

    put: (endpoint, body) =>
      request(endpoint, { method: 'PUT', body }),

    delete: endpoint =>
      request(endpoint, { method: 'DELETE' }),

    // For binary downloads that need response headers (Content-Disposition)
    getBlob: async (endpoint, params) => {
      const response = await requestRaw(endpoint, { method: 'GET', params, responseType: 'blob' })
      return { blob: response._data, headers: response.headers }
    },
  }
}
