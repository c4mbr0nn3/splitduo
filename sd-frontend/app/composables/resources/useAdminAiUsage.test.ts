import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import useAdminAiUsage from './useAdminAiUsage'
import { apiMock } from '~/composables/api/base.mock'
import type { AiAdminConfig, AiUsageEntry, AiUsageSummary, Pagination } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useAdminAiUsage.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n'; mock the module so `t` is a
// controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const pagination: Pagination = {
  page: 2,
  limit: 50,
  total: 120,
  totalPages: 3,
  hasNext: true,
  hasPrev: true,
}

const entry = (overrides: Partial<AiUsageEntry> = {}): AiUsageEntry => ({
  userId: 'user-1',
  feature: 'receipt_parse',
  model: 'test-model',
  success: true,
  requestedAt: 1770076800,
  ...overrides,
})

const summary = (overrides: Partial<AiUsageSummary> = {}): AiUsageSummary => ({
  totalCalls: 2,
  successfulCalls: 1,
  failedCalls: 1,
  successRate: 0.5,
  totalInputTokens: 10,
  totalOutputTokens: 20,
  totalTokens: 30,
  avgLatencyMs: 100,
  byDay: [{ date: '2026-02-03', calls: 2, inputTokens: 10, outputTokens: 20, totalTokens: 30, failed: 1 }],
  byModel: [{ model: 'test-model', calls: 2, inputTokens: 10, outputTokens: 20, totalTokens: 30 }],
  byUser: [{ userId: 'user-1', calls: 2, totalTokens: 30 }],
  ...overrides,
})

const config = (overrides: Partial<AiAdminConfig> = {}): AiAdminConfig => ({
  enabled: false,
  model: null,
  baseUrlHost: null,
  ...overrides,
})

describe('useAdminAiUsage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchUsage', () => {
    it('calls getPaginated, stores entries and pagination, and clears isLoading', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [entry()], pagination })
      const usage = useAdminAiUsage()

      await usage.fetchUsage()

      expect(apiMock.getPaginated).toHaveBeenCalledWith('/admin/ai/usage', { page: 1, limit: 20 })
      expect(usage.entries.value).toEqual([entry()])
      expect(usage.pagination.value).toEqual(pagination)
      expect(usage.isLoadingList.value).toBe(false)
    })

    it('passes success: false through to the API (the != null check must not drop it)', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [entry({ success: false })], pagination })
      const usage = useAdminAiUsage()

      await usage.fetchUsage({ success: false })

      expect(apiMock.getPaginated).toHaveBeenCalledWith(
        '/admin/ai/usage',
        expect.objectContaining({ success: false }),
      )
    })

    it('omits unset filters and forwards from/to/userId when set', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [], pagination })
      const usage = useAdminAiUsage()

      await usage.fetchUsage({ page: 3, limit: 50, from: 100, to: 200, userId: 'u-9', success: true })

      expect(apiMock.getPaginated).toHaveBeenCalledWith('/admin/ai/usage', {
        page: 3, limit: 50, from: 100, to: 200, userId: 'u-9', success: true,
      })
    })

    it('shows an error toast and clears isLoading when the API call fails', async () => {
      apiMock.getPaginated.mockRejectedValue(new Error('Network down'))
      const usage = useAdminAiUsage()

      await usage.fetchUsage()

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.adminAiUsage.loadFailed')
      expect(usage.isLoadingList.value).toBe(false)
    })
  })

  describe('fetchSummary', () => {
    it('calls get without paging params, stores the summary, and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: summary() })
      const usage = useAdminAiUsage()

      await usage.fetchSummary()

      expect(apiMock.get).toHaveBeenCalledWith('/admin/ai/usage/summary', {})
      expect(usage.summary.value).toEqual(summary())
      expect(usage.isLoadingSummary.value).toBe(false)
    })

    it('passes summary filters through to the API', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: summary() })
      const usage = useAdminAiUsage()

      await usage.fetchSummary({ from: 1, to: 2, userId: 'u-1', success: false })

      expect(apiMock.get).toHaveBeenCalledWith(
        '/admin/ai/usage/summary',
        { from: 1, to: 2, userId: 'u-1', success: false },
      )
    })

    it('shows an error toast and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Summary failed'))
      const usage = useAdminAiUsage()

      await usage.fetchSummary()

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.adminAiUsage.summaryLoadFailed')
      expect(usage.isLoadingSummary.value).toBe(false)
    })
  })

  describe('fetchConfig', () => {
    it('calls get for the config endpoint, stores it, and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: config() })
      const usage = useAdminAiUsage()

      await usage.fetchConfig()

      expect(apiMock.get).toHaveBeenCalledWith('/admin/ai/config')
      expect(usage.config.value).toEqual(config())
      expect(usage.isLoadingConfig.value).toBe(false)
    })

    it('shows an error toast and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Config failed'))
      const usage = useAdminAiUsage()

      await usage.fetchConfig()

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.adminAiUsage.configLoadFailed')
      expect(usage.isLoadingConfig.value).toBe(false)
    })
  })
})
