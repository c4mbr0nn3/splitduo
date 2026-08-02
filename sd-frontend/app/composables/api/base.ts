import type { ApiEnvelope, PaginatedEnvelope } from '~/types/domain'

// Shared across all useApi() instances — deduplicates concurrent refresh attempts
let _refreshPromise: Promise<boolean> | null = null

export default function useApi() {
  const config = useRuntimeConfig()
  const { getToken } = useAuthToken()
  const nuxtApp = useNuxtApp()

  const apiConfig = {
    baseURL: import.meta.dev ? 'http://localhost:8080/api/v1' : config.public.apiBaseUrl,
  }

  // Create authenticated request headers
  const getAuthHeaders = (): Record<string, string> => {
    const token = getToken()
    return token
      ? { Authorization: `Bearer ${token}` }
      : {}
  }

  const fetchOnce = (endpoint: string, options: Parameters<typeof $fetch.raw>[1] = {}) => {
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
  const requestRaw = async (endpoint: string, options: Parameters<typeof $fetch.raw>[1] = {}) => {
    try {
      return await fetchOnce(endpoint, options)
    }
    catch (error: unknown) {
      // On 401: attempt one token refresh then retry.
      // Skip /auth/* endpoints to avoid infinite loops.
      if (
        error
        && typeof error === 'object'
        && 'status' in error
        && (error as { status?: number }).status === 401
        && !endpoint.startsWith('/auth/')
      ) {
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
          catch (retryError: unknown) {
            const retryErr = retryError as { status?: number, message?: string }
            throw createError({
              statusCode: retryErr.status || 500,
              statusMessage: retryErr.message || 'API Error',
            })
          }
        }
      }
      const err = error as { status?: number, message?: string }
      throw createError({
        statusCode: err.status || 500,
        statusMessage: err.message || 'API Error',
      })
    }
  }

  // Thin wrapper that returns parsed body only
  const request = async <T>(endpoint: string, options: Parameters<typeof $fetch.raw>[1] = {}): Promise<ApiEnvelope<T>> => {
    return (await requestRaw(endpoint, options))._data as ApiEnvelope<T>
  }

  return {
    get: <T>(endpoint: string, params?: Record<string, unknown>): Promise<ApiEnvelope<T>> =>
      request<T>(endpoint, { method: 'GET', params } as Parameters<typeof $fetch.raw>[1]),

    getPaginated: <T>(endpoint: string, params?: Record<string, unknown>): Promise<PaginatedEnvelope<T>> =>
      request<PaginatedEnvelope<T>>(endpoint, { method: 'GET', params } as Parameters<typeof $fetch.raw>[1]) as unknown as Promise<PaginatedEnvelope<T>>,

    post: <T>(endpoint: string, body?: unknown): Promise<ApiEnvelope<T>> =>
      request<T>(endpoint, { method: 'POST', body } as Parameters<typeof $fetch.raw>[1]),

    put: <T>(endpoint: string, body?: unknown): Promise<ApiEnvelope<T>> =>
      request<T>(endpoint, { method: 'PUT', body } as Parameters<typeof $fetch.raw>[1]),

    delete: (endpoint: string): Promise<ApiEnvelope<undefined>> =>
      request<undefined>(endpoint, { method: 'DELETE' } as Parameters<typeof $fetch.raw>[1]),

    // For binary downloads that need response headers (Content-Disposition)
    getBlob: async (endpoint: string, params?: Record<string, unknown>): Promise<{ blob: Blob, headers: Headers }> => {
      const response = await requestRaw(endpoint, { method: 'GET', params, responseType: 'blob' } as Parameters<typeof $fetch.raw>[1])
      return { blob: response._data as Blob, headers: response.headers }
    },
  }
}
