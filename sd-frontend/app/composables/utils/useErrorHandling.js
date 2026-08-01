export default function useErrorHandling() {
  const { t } = useI18n()
  const { showError } = useNotifications()

  const handleApiError = (error, defaultMessage) => {
    const message = error?.data?.error?.message
      || error?.message
      || defaultMessage
      || t('toasts.errors.default')

    showError(message)
  }

  const handleValidationErrors = (errors) => {
    if (Array.isArray(errors)) {
      errors.forEach((error) => {
        showError(t('toasts.errors.validationError', { field: error.field, message: error.message }))
      })
    }
  }

  const handleAuthError = (error) => {
    if (error.statusCode === 401) {
      showError(t('toasts.errors.authRequired'))
      navigateTo('/')
    }
    else if (error.statusCode === 403) {
      showError(t('toasts.errors.accessDenied'))
    }
    else {
      handleApiError(error)
    }
  }

  return {
    handleApiError,
    handleValidationErrors,
    handleAuthError,
  }
}
