import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref } from 'vue'

import useExpenses from './useExpenses'
import { apiMock } from '~/composables/api/base.mock'
import type { Expense, Pagination, CreateExpenseRequest, UpdateExpenseRequest } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useExpenses.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const expense = (overrides: Partial<Expense> = {}): Expense => ({
  id: 'expense-1',
  groupId: 'group-1',
  title: 'Dinner',
  amount: 42.5,
  paidByUserId: 'user-1',
  paidByUser: { id: 'user-1', firstName: 'Alice' },
  expenseDate: '2026-01-01',
  categoryId: 1,
  paymentModeId: 1,
  splits: [{ id: 'split-1', userId: 'user-1', user: { id: 'user-1', firstName: 'Alice' }, splitAmount: 21.25 }],
  attachmentCount: 0,
  expenseTypeId: 0,
  createdAt: 0,
  updatedAt: 0,
  ...overrides,
})

const pagination = (overrides: Partial<Pagination> = {}): Pagination => ({
  page: 1,
  limit: 20,
  total: 1,
  totalPages: 1,
  hasNext: false,
  hasPrev: false,
  ...overrides,
})

const createExpenseRequest: CreateExpenseRequest = {
  title: 'Dinner',
  amount: 42.5,
  paidByUserId: 'user-1',
  expenseDate: '2026-01-01',
}
const updateExpenseRequest: UpdateExpenseRequest = { title: 'Dinner for two' }

describe('useExpenses', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchExpenses', () => {
    it('stores the expenses and pagination from the response and clears isLoading', async () => {
      const responsePagination = pagination({ page: 2, total: 25, totalPages: 2, hasNext: true, hasPrev: true })
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [expense()], pagination: responsePagination })
      const expenses = useExpenses('group-1')

      await expenses.fetchExpenses({ page: 2, limit: 20 })

      expect(apiMock.getPaginated).toHaveBeenCalledWith('/groups/group-1/expenses', { page: 2, limit: 20 })
      expect(expenses.expenses.value).toEqual([expense()])
      expect(expenses.pagination.value).toEqual(responsePagination)
      expect(expenses.isLoading.value).toBe(false)
    })

    it('passes only the provided filters as query params', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [], pagination: pagination() })
      const expenses = useExpenses('group-1')

      await expenses.fetchExpenses({ startDate: '2026-01-01', search: 'dinner' })

      expect(apiMock.getPaginated).toHaveBeenCalledWith('/groups/group-1/expenses', {
        page: 1,
        limit: 20,
        startDate: '2026-01-01',
        search: 'dinner',
      })
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const expenses = useExpenses('')

      await expenses.fetchExpenses()

      expect(apiMock.getPaginated).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.getPaginated.mockRejectedValue(new Error('Network down'))
      const expenses = useExpenses('group-1')

      await expect(expenses.fetchExpenses()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.expenses.loadFailed')
      expect(expenses.isLoading.value).toBe(false)
    })
  })

  describe('fetchExpense', () => {
    it('stores the expense as currentExpense, returns it, and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: expense() })
      const expenses = useExpenses('group-1')

      const result = await expenses.fetchExpense('expense-1')

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/expenses/expense-1')
      expect(expenses.currentExpense.value).toEqual(expense())
      expect(result).toEqual(expense())
      expect(expenses.isLoading.value).toBe(false)
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const expenses = useExpenses('')

      await expenses.fetchExpense('expense-1')

      expect(apiMock.get).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Load failed'))
      const expenses = useExpenses('group-1')

      await expect(expenses.fetchExpense('expense-1')).rejects.toThrow('Load failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.expenses.loadOneFailed')
      expect(expenses.isLoading.value).toBe(false)
    })
  })

  describe('createExpense', () => {
    it('prepends the new expense, shows a success toast, and returns the expense', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: expense() })
      const expenses = useExpenses('group-1')

      const result = await expenses.createExpense(createExpenseRequest)

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/expenses', createExpenseRequest)
      expect(expenses.expenses.value).toEqual([expense()])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.expenses.created')
      expect(result).toEqual(expense())
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const expenses = useExpenses('')

      await expenses.createExpense(createExpenseRequest)

      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Create failed'))
      const expenses = useExpenses('group-1')

      await expect(expenses.createExpense(createExpenseRequest)).rejects.toThrow('Create failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.expenses.createFailed')
    })
  })

  describe('updateExpense', () => {
    it('updates the expense in the list and as currentExpense, shows a success toast, and returns the expense', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [expense()], pagination: pagination() })
      apiMock.get.mockResolvedValue({ success: true, data: expense() })
      apiMock.put.mockResolvedValue({ success: true, data: expense({ title: 'Dinner for two' }) })
      const expenses = useExpenses('group-1')
      await expenses.fetchExpenses()
      await expenses.fetchExpense('expense-1')

      const result = await expenses.updateExpense('expense-1', updateExpenseRequest)

      expect(apiMock.put).toHaveBeenCalledWith('/groups/group-1/expenses/expense-1', updateExpenseRequest)
      expect(expenses.expenses.value[0]).toMatchObject({ title: 'Dinner for two' })
      expect(expenses.currentExpense.value).toMatchObject({ title: 'Dinner for two' })
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.expenses.updated')
      expect(result).toEqual(expense({ title: 'Dinner for two' }))
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const expenses = useExpenses('')

      await expenses.updateExpense('expense-1', updateExpenseRequest)

      expect(apiMock.put).not.toHaveBeenCalled()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Update failed'))
      const expenses = useExpenses('group-1')

      await expect(expenses.updateExpense('expense-1', updateExpenseRequest)).rejects.toThrow('Update failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.expenses.updateFailed')
    })
  })

  describe('deleteExpense', () => {
    it('removes the expense from the list, clears currentExpense when it matches, and shows a success toast', async () => {
      apiMock.getPaginated.mockResolvedValue({
        success: true,
        data: [expense(), expense({ id: 'expense-2', title: 'Taxi' })],
        pagination: pagination(),
      })
      apiMock.get.mockResolvedValue({ success: true, data: expense() })
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const expenses = useExpenses('group-1')
      await expenses.fetchExpenses()
      await expenses.fetchExpense('expense-1')

      await expenses.deleteExpense('expense-1')

      expect(apiMock.delete).toHaveBeenCalledWith('/groups/group-1/expenses/expense-1')
      expect(expenses.expenses.value).toEqual([expense({ id: 'expense-2', title: 'Taxi' })])
      expect(expenses.currentExpense.value).toBeNull()
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.expenses.deleted')
    })

    it('keeps currentExpense when a different expense is deleted', async () => {
      apiMock.getPaginated.mockResolvedValue({
        success: true,
        data: [expense(), expense({ id: 'expense-2', title: 'Taxi' })],
        pagination: pagination(),
      })
      apiMock.get.mockResolvedValue({ success: true, data: expense() })
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const expenses = useExpenses('group-1')
      await expenses.fetchExpenses()
      await expenses.fetchExpense('expense-1')

      await expenses.deleteExpense('expense-2')

      expect(expenses.expenses.value).toEqual([expense()])
      expect(expenses.currentExpense.value).toEqual(expense())
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const expenses = useExpenses('')

      await expenses.deleteExpense('expense-1')

      expect(apiMock.delete).not.toHaveBeenCalled()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const expenses = useExpenses('group-1')

      await expect(expenses.deleteExpense('expense-1')).rejects.toThrow('Delete failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.expenses.deleteFailed')
    })
  })

  describe('reactive groupId', () => {
    it('uses the current value of a reactive groupId via toRef', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [], pagination: pagination() })
      const groupId = ref('group-1')
      const expenses = useExpenses(groupId)

      await expenses.fetchExpenses()
      expect(apiMock.getPaginated).toHaveBeenLastCalledWith('/groups/group-1/expenses', { page: 1, limit: 20 })

      groupId.value = 'group-2'
      await expenses.fetchExpenses()
      expect(apiMock.getPaginated).toHaveBeenLastCalledWith('/groups/group-2/expenses', { page: 1, limit: 20 })
    })
  })
})
