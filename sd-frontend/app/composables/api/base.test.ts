import { describe, it, expect, vi, beforeEach } from 'vitest'

import useApi from './base'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const fetchRawMock = vi.hoisted(() => ({ raw: vi.fn() }))
const authTokenMock = vi.hoisted(() => ({
  getToken: vi.fn(),
  getRefreshToken: vi.fn(),
  setToken: vi.fn(),
  removeToken: vi.fn(),
}))
const authMock = vi.hoisted(() => ({ refreshToken: vi.fn() }))

// `$fetch` is auto-imported from the virtual '#build/fetch.mjs', which re-exports
// ofetch's `$fetch` — mock the underlying 'ofetch' module at the boundary.
vi.mock('ofetch', () => ({
  $fetch: {
    raw: fetchRawMock.raw,
    create: () => ({ raw: fetchRawMock.raw }),
  },
}))

// useAuthToken / useAuth are auto-imported inside base.ts; mock the composable
// modules so the 401-refresh flow is fully controlled from the test.
vi.mock('~/composables/auth/useAuthToken', () => ({ default: () => authTokenMock }))
vi.mock('~/composables/auth/useAuth', () => ({ default: () => authMock }))

interface TestGroup {
  id: number
  name: string
}

const okEnvelope = (data: unknown) => ({ _data: data, headers: new Headers() })
const unauthorizedError = { status: 401, message: 'Unauthorized' }

describe('useApi', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    authTokenMock.getToken.mockReturnValue(null)
    authMock.refreshToken.mockResolvedValue(true)
  })

  describe('request methods', () => {
    it('get() calls $fetch.raw with GET, the endpoint appended to the base URL, and params', async () => {
      fetchRawMock.raw.mockResolvedValue(okEnvelope({ success: true, data: { id: 1, name: 'Test' } }))
      const api = useApi()

      const result = await api.get<TestGroup>('/groups', { page: 1 })

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups',
        expect.objectContaining({ method: 'GET', params: { page: 1 } }),
      )
      expect(result).toEqual({ success: true, data: { id: 1, name: 'Test' } })
    })

    it('getPaginated() calls $fetch.raw with GET and returns the paginated envelope', async () => {
      const paginated = {
        success: true,
        data: [{ id: 1, name: 'Test' }],
        pagination: { page: 1, limit: 20, total: 1, totalPages: 1, hasNext: false, hasPrev: false },
      }
      fetchRawMock.raw.mockResolvedValue(okEnvelope(paginated))
      const api = useApi()

      const result = await api.getPaginated<TestGroup>('/groups', { page: 1 })

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups',
        expect.objectContaining({ method: 'GET', params: { page: 1 } }),
      )
      expect(result).toEqual(paginated)
    })

    it('post() calls $fetch.raw with POST and the body', async () => {
      fetchRawMock.raw.mockResolvedValue(okEnvelope({ success: true, data: { id: 1, name: 'New' } }))
      const api = useApi()

      const result = await api.post<TestGroup>('/groups', { name: 'New' })

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups',
        expect.objectContaining({ method: 'POST', body: { name: 'New' } }),
      )
      expect(result).toEqual({ success: true, data: { id: 1, name: 'New' } })
    })

    it('put() calls $fetch.raw with PUT and the body', async () => {
      fetchRawMock.raw.mockResolvedValue(okEnvelope({ success: true, data: { id: 1, name: 'Renamed' } }))
      const api = useApi()

      const result = await api.put<TestGroup>('/groups/1', { name: 'Renamed' })

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups/1',
        expect.objectContaining({ method: 'PUT', body: { name: 'Renamed' } }),
      )
      expect(result).toEqual({ success: true, data: { id: 1, name: 'Renamed' } })
    })

    it('delete() calls $fetch.raw with DELETE', async () => {
      fetchRawMock.raw.mockResolvedValue(okEnvelope({ success: true, data: null }))
      const api = useApi()

      const result = await api.delete('/groups/1')

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups/1',
        expect.objectContaining({ method: 'DELETE' }),
      )
      expect(result).toEqual({ success: true, data: null })
    })
  })

  describe('auth headers', () => {
    it('sends Authorization: Bearer <token> when a token is present', async () => {
      authTokenMock.getToken.mockReturnValue('tok-123')
      fetchRawMock.raw.mockResolvedValue(okEnvelope({ success: true, data: null }))
      const api = useApi()

      await api.get('/groups')

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups',
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: 'Bearer tok-123' }),
        }),
      )
    })

    it('omits the Authorization header when no token is present', async () => {
      fetchRawMock.raw.mockResolvedValue(okEnvelope({ success: true, data: null }))
      const api = useApi()

      await api.get('/groups')

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups',
        expect.objectContaining({ headers: { 'Accept-Language': 'en' } }),
      )
    })

    it('sends the current locale as Accept-Language', async () => {
      fetchRawMock.raw.mockResolvedValue(okEnvelope({ success: true, data: null }))
      const api = useApi()

      await api.get('/groups')

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups',
        expect.objectContaining({
          headers: expect.objectContaining({ 'Accept-Language': 'en' }),
        }),
      )
    })
  })

  describe('401 refresh flow', () => {
    it('refreshes once and retries the request after a 401', async () => {
      fetchRawMock.raw
        .mockRejectedValueOnce(unauthorizedError)
        .mockResolvedValueOnce(okEnvelope({ success: true, data: { id: 1, name: 'Test' } }))
      const api = useApi()

      const result = await api.get<TestGroup>('/groups')

      expect(authMock.refreshToken).toHaveBeenCalledTimes(1)
      expect(fetchRawMock.raw).toHaveBeenCalledTimes(2)
      expect(result).toEqual({ success: true, data: { id: 1, name: 'Test' } })
    })

    it('throws createError when the refresh fails after a 401', async () => {
      authMock.refreshToken.mockResolvedValue(false)
      fetchRawMock.raw.mockRejectedValueOnce(unauthorizedError)
      const api = useApi()

      await expect(api.get<TestGroup>('/groups')).rejects.toMatchObject({
        statusCode: 401,
        statusMessage: 'Unauthorized',
      })

      expect(authMock.refreshToken).toHaveBeenCalledTimes(1)
      expect(fetchRawMock.raw).toHaveBeenCalledTimes(1)
    })

    it('throws createError when the retry after a successful refresh also fails', async () => {
      fetchRawMock.raw
        .mockRejectedValueOnce(unauthorizedError)
        .mockRejectedValueOnce({ status: 503, message: 'Still down' })
      const api = useApi()

      await expect(api.get<TestGroup>('/groups')).rejects.toMatchObject({
        statusCode: 503,
        statusMessage: 'Still down',
      })

      expect(authMock.refreshToken).toHaveBeenCalledTimes(1)
      expect(fetchRawMock.raw).toHaveBeenCalledTimes(2)
    })

    it('does not attempt a refresh for /auth/* endpoints', async () => {
      fetchRawMock.raw.mockRejectedValueOnce(unauthorizedError)
      const api = useApi()

      await expect(api.post('/auth/refresh', { token: 'expired' })).rejects.toMatchObject({
        statusCode: 401,
        statusMessage: 'Unauthorized',
      })

      expect(authMock.refreshToken).not.toHaveBeenCalled()
    })

    it('propagates non-401 errors without attempting a refresh', async () => {
      fetchRawMock.raw.mockRejectedValueOnce({ status: 500, message: 'Server exploded' })
      const api = useApi()

      await expect(api.get<TestGroup>('/groups')).rejects.toMatchObject({
        statusCode: 500,
        statusMessage: 'Server exploded',
      })

      expect(authMock.refreshToken).not.toHaveBeenCalled()
    })

    it('deduplicates concurrent 401s into a single refresh call', async () => {
      let resolveRefresh: (value: boolean) => void = () => {}
      const refreshPromise = new Promise<boolean>((resolve) => {
        resolveRefresh = resolve
      })
      authMock.refreshToken.mockReturnValue(refreshPromise)
      fetchRawMock.raw
        .mockRejectedValueOnce(unauthorizedError)
        .mockRejectedValueOnce(unauthorizedError)
        .mockResolvedValueOnce(okEnvelope({ success: true, data: { id: 1, name: 'A' } }))
        .mockResolvedValueOnce(okEnvelope({ success: true, data: { id: 2, name: 'B' } }))
      const api = useApi()

      const first = api.get<TestGroup>('/groups')
      const second = api.get<TestGroup>('/groups')

      // Both requests hit the 401 path and await the shared refresh promise.
      await vi.waitFor(() => expect(authMock.refreshToken).toHaveBeenCalledTimes(1))

      resolveRefresh(true)
      const [firstResult, secondResult] = await Promise.all([first, second])

      expect(firstResult).toEqual({ success: true, data: { id: 1, name: 'A' } })
      expect(secondResult).toEqual({ success: true, data: { id: 2, name: 'B' } })
      expect(fetchRawMock.raw).toHaveBeenCalledTimes(4)
    })
  })

  describe('getBlob', () => {
    it('returns { blob, headers } from the raw response', async () => {
      const headers = new Headers({ 'content-disposition': 'attachment; filename="export.csv"' })
      fetchRawMock.raw.mockResolvedValue({ _data: new Blob(['a,b,c']), headers })
      const api = useApi()

      const result = await api.getBlob('/groups/export', { format: 'csv' })

      expect(result.blob).toBeInstanceOf(Blob)
      expect(result.headers.get('content-disposition')).toBe('attachment; filename="export.csv"')
    })

    it('passes responseType: blob to $fetch.raw', async () => {
      fetchRawMock.raw.mockResolvedValue({ _data: new Blob(), headers: new Headers() })
      const api = useApi()

      await api.getBlob('/groups/export')

      expect(fetchRawMock.raw).toHaveBeenCalledWith(
        '/api/v1/groups/export',
        expect.objectContaining({ method: 'GET', responseType: 'blob' }),
      )
    })
  })
})
