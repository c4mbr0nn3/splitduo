import type { ExpenseAttachment } from '~/types/domain'

export default function useExpenseAttachments(groupId: string | Ref<string>, expenseId: string | Ref<string>) {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const expenseIdRef = toRef(expenseId)
  const attachments = ref<ExpenseAttachment[]>([])
  const isLoading = ref(false)

  const fetchAttachments = async () => {
    if (!groupIdRef.value || !expenseIdRef.value) return
    isLoading.value = true
    try {
      const response = await api.get<ExpenseAttachment[]>(`/groups/${groupIdRef.value}/expenses/${expenseIdRef.value}/attachments`)
      if (response.success && response.data) attachments.value = response.data
    }
    catch (error: unknown) {
      showError(t('toasts.attachments.loadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const uploadAttachment = async (file: File): Promise<ExpenseAttachment | null> => {
    if (!groupIdRef.value || !expenseIdRef.value) return null
    isLoading.value = true
    try {
      const formData = new FormData()
      formData.append('file', file, file.name)
      const response = await api.post<ExpenseAttachment>(`/groups/${groupIdRef.value}/expenses/${expenseIdRef.value}/attachments`, formData)
      if (response.success && response.data) {
        attachments.value.push(response.data)
        showSuccess(t('toasts.attachments.uploaded'))
        return response.data
      }
      return null
    }
    catch (error: unknown) {
      showError(t('toasts.attachments.uploadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  const downloadAttachment = async (attachment: ExpenseAttachment): Promise<void> => {
    if (!groupIdRef.value || !expenseIdRef.value) return
    try {
      const { blob } = await api.getBlob(`/groups/${groupIdRef.value}/expenses/${expenseIdRef.value}/attachments/${attachment.id}`)
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = attachment.filenameOriginal
      document.body.appendChild(a)
      a.click()
      window.URL.revokeObjectURL(url)
      document.body.removeChild(a)
    }
    catch (error: unknown) {
      showError(t('toasts.attachments.downloadFailed'))
      throw error
    }
  }

  const getAttachmentUrl = async (attachment: ExpenseAttachment): Promise<string | null> => {
    // For image preview: fetch blob, create object URL (caller must revoke)
    if (!groupIdRef.value || !expenseIdRef.value) return null
    try {
      const { blob } = await api.getBlob(`/groups/${groupIdRef.value}/expenses/${expenseIdRef.value}/attachments/${attachment.id}`)
      return window.URL.createObjectURL(blob)
    }
    catch {
      showError(t('toasts.attachments.loadFailed'))
      return null
    }
  }

  const removeAttachment = async (attachmentId: string): Promise<void> => {
    if (!groupIdRef.value || !expenseIdRef.value) return
    try {
      await api.delete(`/groups/${groupIdRef.value}/expenses/${expenseIdRef.value}/attachments/${attachmentId}`)
      attachments.value = attachments.value.filter(a => a.id !== attachmentId)
      showSuccess(t('toasts.attachments.deleted'))
    }
    catch (error: unknown) {
      showError(t('toasts.attachments.deleteFailed'))
      throw error
    }
  }

  return {
    attachments: readonly(attachments),
    isLoading: readonly(isLoading),
    fetchAttachments,
    uploadAttachment,
    downloadAttachment,
    getAttachmentUrl,
    removeAttachment,
  }
}
