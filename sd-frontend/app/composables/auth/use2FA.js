export default function use2FA() {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()
  const isLoading = ref(false)

  // POST /2fa/setup/initiate → { secret, qrCodeUri, backupCodes }
  const initiateSetup = async () => {
    isLoading.value = true
    try {
      const response = await api.post('/2fa/setup/initiate')
      if (response.success && response.data) {
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.2fa.initiateFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // POST /2fa/setup/verify → body: { code }
  const verifySetup = async (code) => {
    isLoading.value = true
    try {
      const response = await api.post('/2fa/setup/verify', { code })
      if (response.success) {
        showSuccess(t('toasts.2fa.enabled'))
        return true
      }
    }
    catch (error) {
      showError(t('toasts.2fa.invalidCode'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // POST /2fa/disable → body: { password }
  const disable = async (password) => {
    isLoading.value = true
    try {
      const response = await api.post('/2fa/disable', { password })
      if (response.success) {
        showSuccess(t('toasts.2fa.disabled'))
        return true
      }
    }
    catch (error) {
      showError(t('toasts.2fa.disableFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // POST /2fa/backup-codes/generate → string[]
  const generateBackupCodes = async () => {
    isLoading.value = true
    try {
      const response = await api.post('/2fa/backup-codes/generate')
      if (response.success && response.data) {
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.2fa.backupCodesFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  return {
    isLoading: readonly(isLoading),
    initiateSetup,
    verifySetup,
    disable,
    generateBackupCodes,
  }
}
