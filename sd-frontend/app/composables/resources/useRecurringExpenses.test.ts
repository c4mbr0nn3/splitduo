import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref } from 'vue'

import useRecurringExpenses from './useRecurringExpenses'
import { apiMock } from '~/composables/api/base.mock'
import type { RecurringExpenseTemplate, RecurringExpenseInstance, Pagination, CreateRecurringExpenseTemplateRequest } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useRecurringExpenses.ts;
// mock the composable modules so every API call and toast is controlled from
// the test. useI18n is auto-imported from 'vue-i18n'; mock the module so `t`
// is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const template = (overrides: Partial<RecurringExpenseTemplate> = {}): RecurringExpenseTemplate => ({
  id: 'template-1',
  groupId: 'group-1',
  ownerId: 'user-1',
  title: 'Rent',
  amount: 800,
  paidByUserId: 'user-1',
  recurrenceMode: 1,
  weekdays: 0,
  anchorDate: '2026-01-01',
  isActive: true,
  ...overrides,
})

const instance = (overrides: Partial<RecurringExpenseInstance> = {}): RecurringExpenseInstance => ({
  id: 'instance-1',
  templateId: 'template-1',
  templateTitle: 'Rent',
  periodDate: '2026-01-01',
  status: 1,
  amount: 800,
  createdAt: 0,
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

const createTemplateRequest: CreateRecurringExpenseTemplateRequest = {
  title: 'Rent',
  amount: 800,
  paidByUserId: 'user-1',
  recurrenceMode: 1,
  weekdays: 0,
  anchorDate: '2026-01-01',
}

describe('useRecurringExpenses', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchTemplates', () => {
    it('stores the templates from the response and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [template()] })
      const recurring = useRecurringExpenses('group-1')

      await recurring.fetchTemplates()

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/recurring-expenses', undefined)
      expect(recurring.templates.value).toEqual([template()])
      expect(recurring.isLoading.value).toBe(false)
    })

    it('passes includeInactive=true as a query param when requested', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [] })
      const recurring = useRecurringExpenses('group-1')

      await recurring.fetchTemplates(true)

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/recurring-expenses', { includeInactive: true })
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const recurring = useRecurringExpenses('')

      await recurring.fetchTemplates()

      expect(apiMock.get).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Network down'))
      const recurring = useRecurringExpenses('group-1')

      await expect(recurring.fetchTemplates()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.recurringExpenses.templatesLoadFailed')
      expect(recurring.isLoading.value).toBe(false)
    })
  })

  describe('createTemplate', () => {
    it('prepends the new template, shows a success toast, and returns the template', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [template({ id: 'template-2' })] })
      apiMock.post.mockResolvedValue({ success: true, data: template() })
      const recurring = useRecurringExpenses('group-1')
      await recurring.fetchTemplates()

      const result = await recurring.createTemplate(createTemplateRequest)

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/recurring-expenses', createTemplateRequest)
      expect(recurring.templates.value).toEqual([template(), template({ id: 'template-2' })])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.recurringExpenses.templateCreated')
      expect(result).toEqual(template())
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Create failed'))
      const recurring = useRecurringExpenses('group-1')

      await expect(recurring.createTemplate(createTemplateRequest)).rejects.toThrow('Create failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.recurringExpenses.templateCreateFailed')
    })
  })

  describe('deleteTemplate', () => {
    it('removes the template from the list and shows a success toast', async () => {
      apiMock.get.mockResolvedValue({
        success: true,
        data: [template(), template({ id: 'template-2', title: 'Wifi' })],
      })
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const recurring = useRecurringExpenses('group-1')
      await recurring.fetchTemplates()

      await recurring.deleteTemplate('template-1')

      expect(apiMock.delete).toHaveBeenCalledWith('/groups/group-1/recurring-expenses/template-1')
      expect(recurring.templates.value).toEqual([template({ id: 'template-2', title: 'Wifi' })])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.recurringExpenses.templateDeleted')
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const recurring = useRecurringExpenses('group-1')

      await expect(recurring.deleteTemplate('template-1')).rejects.toThrow('Delete failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.recurringExpenses.templateDeleteFailed')
    })
  })

  describe('fetchPendingInstances', () => {
    it('fetches group-wide instances with the PendingApproval status filter and stores pagination', async () => {
      const responsePagination = pagination({ page: 2, total: 25, totalPages: 2, hasNext: true, hasPrev: true })
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [instance()], pagination: responsePagination })
      const recurring = useRecurringExpenses('group-1')

      await recurring.fetchPendingInstances(2, 10)

      expect(apiMock.getPaginated).toHaveBeenCalledWith('/groups/group-1/recurring-expenses/instances', {
        page: 2,
        limit: 10,
        status: 'PendingApproval',
      })
      expect(recurring.pendingInstances.value).toEqual([instance()])
      expect(recurring.pagination.value).toEqual(responsePagination)
      expect(recurring.isLoading.value).toBe(false)
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.getPaginated.mockRejectedValue(new Error('Network down'))
      const recurring = useRecurringExpenses('group-1')

      await expect(recurring.fetchPendingInstances()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.recurringExpenses.instancesLoadFailed')
    })
  })

  describe('approveInstance', () => {
    it('posts to the approve endpoint, removes the instance from pending, and shows a success toast', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [instance()], pagination: pagination() })
      apiMock.post.mockResolvedValue({ success: true, data: { id: 'expense-1' } })
      const recurring = useRecurringExpenses('group-1')
      await recurring.fetchPendingInstances()

      await recurring.approveInstance('instance-1')

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/recurring-expenses/instances/instance-1/approve', undefined)
      expect(recurring.pendingInstances.value).toEqual([])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.recurringExpenses.instanceApproved')
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Approve failed'))
      const recurring = useRecurringExpenses('group-1')

      await expect(recurring.approveInstance('instance-1')).rejects.toThrow('Approve failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.recurringExpenses.instanceApproveFailed')
    })
  })

  describe('rejectInstance', () => {
    it('posts to the reject endpoint, removes the instance from pending, and shows a success toast', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [instance()], pagination: pagination() })
      apiMock.post.mockResolvedValue({ success: true, data: null })
      const recurring = useRecurringExpenses('group-1')
      await recurring.fetchPendingInstances()

      await recurring.rejectInstance('instance-1')

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/recurring-expenses/instances/instance-1/reject')
      expect(recurring.pendingInstances.value).toEqual([])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.recurringExpenses.instanceRejected')
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Reject failed'))
      const recurring = useRecurringExpenses('group-1')

      await expect(recurring.rejectInstance('instance-1')).rejects.toThrow('Reject failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.recurringExpenses.instanceRejectFailed')
    })
  })

  describe('resumeTemplate', () => {
    it('posts the skip strategy to the resume endpoint and updates the template in state', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [template({ isActive: false, missedCount: 3 })] })
      apiMock.post.mockResolvedValue({
        success: true,
        data: template({ isActive: true, missedCount: 0, resumeFrom: '2026-01-08' }),
      })
      const recurring = useRecurringExpenses('group-1')
      await recurring.fetchTemplates()

      const result = await recurring.resumeTemplate('template-1', 'skip')

      expect(apiMock.post).toHaveBeenCalledWith(
        '/groups/group-1/recurring-expenses/template-1/resume',
        { strategy: 'skip' },
      )
      expect(recurring.templates.value).toEqual([template({ isActive: true, missedCount: 0, resumeFrom: '2026-01-08' })])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.recurringExpenses.templateResumed')
      expect(result).toEqual(template({ isActive: true, missedCount: 0, resumeFrom: '2026-01-08' }))
    })

    it('posts the backfill strategy to the resume endpoint and updates the template in state', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [template({ isActive: false, missedCount: 3 })] })
      apiMock.post.mockResolvedValue({
        success: true,
        data: template({ isActive: true, missedCount: 0, resumeFrom: null }),
      })
      const recurring = useRecurringExpenses('group-1')
      await recurring.fetchTemplates()

      const result = await recurring.resumeTemplate('template-1', 'backfill')

      expect(apiMock.post).toHaveBeenCalledWith(
        '/groups/group-1/recurring-expenses/template-1/resume',
        { strategy: 'backfill' },
      )
      expect(recurring.templates.value).toEqual([template({ isActive: true, missedCount: 0, resumeFrom: null })])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.recurringExpenses.templateResumed')
      expect(result).toEqual(template({ isActive: true, missedCount: 0, resumeFrom: null }))
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Resume failed'))
      const recurring = useRecurringExpenses('group-1')

      await expect(recurring.resumeTemplate('template-1', 'skip')).rejects.toThrow('Resume failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.recurringExpenses.templateResumeFailed')
    })
  })

  describe('reactive groupId', () => {
    it('uses the current value of a reactive groupId via toRef', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [] })
      const groupId = ref('group-1')
      const recurring = useRecurringExpenses(groupId)

      await recurring.fetchTemplates()
      expect(apiMock.get).toHaveBeenLastCalledWith('/groups/group-1/recurring-expenses', undefined)

      groupId.value = 'group-2'
      await recurring.fetchTemplates()
      expect(apiMock.get).toHaveBeenLastCalledWith('/groups/group-2/recurring-expenses', undefined)
    })
  })
})
