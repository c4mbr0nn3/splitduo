import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import useExpenseAttachments from './useExpenseAttachments'
import { apiMock } from '~/composables/api/base.mock'
import type { ExpenseAttachment } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useExpenseAttachments.ts;
// mock the composable modules so every API call and toast is controlled from
// the test. useI18n is auto-imported from 'vue-i18n'; mock the module so `t`
// is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const attachment = (overrides: Partial<ExpenseAttachment> = {}): ExpenseAttachment => ({
  id: 'attachment-1',
  expenseId: 'expense-1',
  filenameOriginal: 'receipt.jpg',
  mimeType: 'image/jpeg',
  sizeBytes: 1024,
  createdAt: 0,
  updatedAt: 0,
  ...overrides,
})

const uploadFile = (): File => new File(['fake-image-bytes'], 'receipt.jpg', { type: 'image/jpeg' })

describe('useExpenseAttachments', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchAttachments', () => {
    it('stores the attachments from the response and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [attachment()] })
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      await attachments.fetchAttachments()

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/expenses/expense-1/attachments')
      expect(attachments.attachments.value).toEqual([attachment()])
      expect(attachments.isLoading.value).toBe(false)
    })

    it('returns early without calling the API when groupId or expenseId is empty', async () => {
      const attachments = useExpenseAttachments('', 'expense-1')

      await attachments.fetchAttachments()

      expect(apiMock.get).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Network down'))
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      await expect(attachments.fetchAttachments()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.attachments.loadFailed')
      expect(attachments.isLoading.value).toBe(false)
    })
  })

  describe('uploadAttachment', () => {
    it('posts the file as multipart form-data, appends the attachment, shows a success toast, and returns it', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: attachment() })
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      const result = await attachments.uploadAttachment(uploadFile())

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/expenses/expense-1/attachments', expect.any(FormData))
      const formData = apiMock.post.mock.calls[0]?.[1] as FormData
      expect(formData.get('file')).toBeInstanceOf(File)
      expect(attachments.attachments.value).toEqual([attachment()])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.attachments.uploaded')
      expect(result).toEqual(attachment())
    })

    it('returns null without calling the API when groupId or expenseId is empty', async () => {
      const attachments = useExpenseAttachments('', 'expense-1')

      const result = await attachments.uploadAttachment(uploadFile())

      expect(apiMock.post).not.toHaveBeenCalled()
      expect(result).toBeNull()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Upload failed'))
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      await expect(attachments.uploadAttachment(uploadFile())).rejects.toThrow('Upload failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.attachments.uploadFailed')
    })
  })

  describe('removeAttachment', () => {
    it('removes the attachment from the list and shows a success toast', async () => {
      apiMock.get.mockResolvedValue({
        success: true,
        data: [attachment(), attachment({ id: 'attachment-2', filenameOriginal: 'receipt2.jpg' })],
      })
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const attachments = useExpenseAttachments('group-1', 'expense-1')
      await attachments.fetchAttachments()

      await attachments.removeAttachment('attachment-1')

      expect(apiMock.delete).toHaveBeenCalledWith('/groups/group-1/expenses/expense-1/attachments/attachment-1')
      expect(attachments.attachments.value).toEqual([attachment({ id: 'attachment-2', filenameOriginal: 'receipt2.jpg' })])
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.attachments.deleted')
    })

    it('returns early without calling the API when groupId or expenseId is empty', async () => {
      const attachments = useExpenseAttachments('', 'expense-1')

      await attachments.removeAttachment('attachment-1')

      expect(apiMock.delete).not.toHaveBeenCalled()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      await expect(attachments.removeAttachment('attachment-1')).rejects.toThrow('Delete failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.attachments.deleteFailed')
    })
  })

  describe('downloadAttachment', () => {
    it('fetches the blob, creates an object URL, and triggers a download', async () => {
      const blob = new Blob(['fake-pdf-bytes'], { type: 'application/pdf' })
      apiMock.getBlob.mockResolvedValue({ blob, headers: new Headers() })
      const createObjectURLSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake-download')
      const revokeObjectURLSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
      const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      await attachments.downloadAttachment(attachment({ filenameOriginal: 'receipt.pdf', mimeType: 'application/pdf' }))

      expect(apiMock.getBlob).toHaveBeenCalledWith('/groups/group-1/expenses/expense-1/attachments/attachment-1')
      expect(createObjectURLSpy).toHaveBeenCalledWith(blob)
      expect(clickSpy).toHaveBeenCalled()
      expect(revokeObjectURLSpy).toHaveBeenCalledWith('blob:fake-download')
      createObjectURLSpy.mockRestore()
      revokeObjectURLSpy.mockRestore()
      clickSpy.mockRestore()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.getBlob.mockRejectedValue(new Error('Download failed'))
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      await expect(attachments.downloadAttachment(attachment())).rejects.toThrow('Download failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.attachments.downloadFailed')
    })
  })

  describe('getAttachmentUrl', () => {
    it('fetches the blob and returns an object URL', async () => {
      const blob = new Blob(['fake-image-bytes'], { type: 'image/jpeg' })
      apiMock.getBlob.mockResolvedValue({ blob, headers: new Headers() })
      const createObjectURLSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake-preview')
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      const url = await attachments.getAttachmentUrl(attachment())

      expect(apiMock.getBlob).toHaveBeenCalledWith('/groups/group-1/expenses/expense-1/attachments/attachment-1')
      expect(createObjectURLSpy).toHaveBeenCalledWith(blob)
      expect(url).toBe('blob:fake-preview')
      createObjectURLSpy.mockRestore()
    })

    it('shows an error toast and returns null when the API call fails', async () => {
      apiMock.getBlob.mockRejectedValue(new Error('Load failed'))
      const attachments = useExpenseAttachments('group-1', 'expense-1')

      const url = await attachments.getAttachmentUrl(attachment())

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.attachments.loadFailed')
      expect(url).toBeNull()
    })
  })
})
