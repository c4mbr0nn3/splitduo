export default function useApi() {
  const config = useRuntimeConfig()
  const { getToken } = useAuthToken()

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

  // Base request function with error handling
  const request = async (endpoint, options = {}) => {
    try {
      const response = await $fetch(
        `${apiConfig.baseURL}${endpoint}`,
        {
          headers: {
            // 'Content-Type': 'application/json',
            ...getAuthHeaders(),
            ...(options.headers || {}),
          },
          ...options,
        },
      )
      return response
    }
    catch (error) {
      // Handle different error types
      throw createError({
        statusCode: error.status || 500,
        statusMessage: error.message || 'API Error',
      })
    }
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
  }
}
