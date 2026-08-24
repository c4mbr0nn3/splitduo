export default function useUserAvatar() {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const isLoading = ref(false)

  // Upload/replace current user's avatar
  async function uploadAvatar(file: File): Promise<boolean> {
    isLoading.value = true
    try {
      const formData = new FormData()
      formData.append('file', file, file.name)
      await api.put(`/users/me/avatar`, formData)
      showSuccess(t('toasts.avatar.uploaded'))
      return true
    }
    catch (error: unknown) {
      showError(t('toasts.avatar.uploadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Delete current user's avatar
  async function deleteAvatar(): Promise<boolean> {
    isLoading.value = true
    try {
      await api.delete(`/users/me/avatar`)
      showSuccess(t('toasts.avatar.deleted'))
      return true
    }
    catch (error: unknown) {
      showError(t('toasts.avatar.deleteFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get any user's avatar as a blob URL (caller must revoke)
  async function getAvatarUrl(userId: string): Promise<string | null> {
    if (!userId) return null
    try {
      const { blob } = await api.getBlob(`/users/${userId}/avatar`)
      return window.URL.createObjectURL(blob)
    }
    catch {
      // Avatar may not exist (404) — return null silently, no toast
      return null
    }
  }

  return {
    isLoading: readonly(isLoading),
    uploadAvatar,
    deleteAvatar,
    getAvatarUrl,
  }
}
