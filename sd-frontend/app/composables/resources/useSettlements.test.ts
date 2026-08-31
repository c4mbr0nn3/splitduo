import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref } from 'vue'

import useSettlements from './useSettlements'
import { apiMock } from '~/composables/api/base.mock'
import type { Settlement, Pagination, CreateSettlementRequest } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useSettlements.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const settlement = (overrides: Partial<Settlement> = {}): Settlement => ({
  id: 'settlement-1',
  groupId: 'group-1',
  fromUserId: 'user-1',
  fromUser: { id: 'user-1', firstName: 'Alice' },
  amount: 50,
  date: '2026-01-01',
  expenseTypeId: 1,
  paymentModeId: 4,
  createdAt: 0,
  updatedAt: 0,
  ...overrides,
})

const pagination = (overrides: Partial<Pagination> = {}): Pagination => ({
  page: 1,
  limit: 10,
  total: 1,
  totalPages: 1,
  hasNext: false,
  hasPrev: false,
  ...overrides,
})

const createSettlementRequest: CreateSettlementRequest = {
  fromUserId: 'user-1',
  toUserId: 'user-2',
  amount: 50,
  date: '2026-01-01',
}

describe('useSettlements', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchSettlements', () => {
    it('stores the settlements and pagination from the response and clears isLoading', async () => {
      const responsePagination = pagination({ page: 2, total: 25, totalPages: 2, hasNext: true, hasPrev: true })
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [settlement()], pagination: responsePagination })
      const settlements = useSettlements('group-1')

      await settlements.fetchSettlements({ page: 2, limit: 10 })

      expect(apiMock.getPaginated).toHaveBeenCalledWith('/groups/group-1/settlements', { page: 2, limit: 10 })
      expect(settlements.settlements.value).toEqual([settlement()])
      expect(settlements.pagination.value).toEqual(responsePagination)
      expect(settlements.isLoading.value).toBe(false)
    })

    it('is called with default page and limit when no filters are provided', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [], pagination: pagination() })
      const settlements = useSettlements('group-1')

      await settlements.fetchSettlements()

      expect(apiMock.getPaginated).toHaveBeenCalledWith('/groups/group-1/settlements', { page: 1, limit: 10 })
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const settlements = useSettlements('')

      await settlements.fetchSettlements()

      expect(apiMock.getPaginated).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.getPaginated.mockRejectedValue(new Error('Network down'))
      const settlements = useSettlements('group-1')

      await expect(settlements.fetchSettlements()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.settlements.loadFailed')
      expect(settlements.isLoading.value).toBe(false)
    })
  })

  describe('createSettlement', () => {
    it('prepends the new settlement, shows a success toast, and returns the settlement', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [settlement({ id: 'settlement-2' })], pagination: pagination() })
      apiMock.post.mockResolvedValue({ success: true, data: settlement() })
      const settlements = useSettlements('group-1')
      await settlements.fetchSettlements()

      const result = await settlements.createSettlement(createSettlementRequest)

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/settlements', createSettlementRequest)
      expect(settlements.settlements.value).toEqual([settlement(), settlement({ id: 'settlement-2' })])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.settlements.created')
      expect(result).toEqual(settlement())
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const settlements = useSettlements('')

      await settlements.createSettlement(createSettlementRequest)

      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Create failed'))
      const settlements = useSettlements('group-1')

      await expect(settlements.createSettlement(createSettlementRequest)).rejects.toThrow('Create failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.settlements.createFailed')
    })
  })

  describe('deleteSettlement', () => {
    it('removes the settlement from the list and shows a success toast', async () => {
      apiMock.getPaginated.mockResolvedValue({
        success: true,
        data: [settlement(), settlement({ id: 'settlement-2', amount: 10 })],
        pagination: pagination(),
      })
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const settlements = useSettlements('group-1')
      await settlements.fetchSettlements()

      await settlements.deleteSettlement('settlement-1')

      expect(apiMock.delete).toHaveBeenCalledWith('/groups/group-1/settlements/settlement-1')
      expect(settlements.settlements.value).toEqual([settlement({ id: 'settlement-2', amount: 10 })])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.settlements.deleted')
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const settlements = useSettlements('')

      await settlements.deleteSettlement('settlement-1')

      expect(apiMock.delete).not.toHaveBeenCalled()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const settlements = useSettlements('group-1')

      await expect(settlements.deleteSettlement('settlement-1')).rejects.toThrow('Delete failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.settlements.deleteFailed')
    })
  })

  describe('reactive groupId', () => {
    it('uses the current value of a reactive groupId via toRef', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [], pagination: pagination() })
      const groupId = ref('group-1')
      const settlements = useSettlements(groupId)

      await settlements.fetchSettlements()
      expect(apiMock.getPaginated).toHaveBeenLastCalledWith('/groups/group-1/settlements', { page: 1, limit: 10 })

      groupId.value = 'group-2'
      await settlements.fetchSettlements()
      expect(apiMock.getPaginated).toHaveBeenLastCalledWith('/groups/group-2/settlements', { page: 1, limit: 10 })
    })
  })
})
