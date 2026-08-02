export default function useErrorHandling() {
  const { t } = useI18n()
  const { showError } = useNotifications()

  const handleApiError = (error: unknown, defaultMessage?: string): void => {
    let message: string | null = null

    if (typeof error === 'object' && error !== null) {
      const err = error as Record<string, unknown>
      const data = err.data
      if (typeof data === 'object' && data !== null) {
        const dataObj = data as Record<string, unknown>
        const errorObj = dataObj.error
        if (typeof errorObj === 'object' && errorObj !== null) {
          const errorData = errorObj as Record<string, unknown>
          if (typeof errorData.message === 'string') {
            message = errorData.message
          }
        }
      }
      if (message === null && typeof err.message === 'string') {
        message = err.message
      }
    }

    showError(message || defaultMessage || t('toasts.errors.default'))
  }

  const handleValidationErrors = (errors: unknown): void => {
    if (Array.isArray(errors)) {
      errors.forEach((error: unknown) => {
        const err = error as { field?: string, message?: string }
        showError(t('toasts.errors.validationError', { field: err.field ?? '', message: err.message ?? '' }))
      })
    }
  }

  const handleAuthError = (error: unknown): void => {
    if (typeof error === 'object' && error !== null && 'statusCode' in error) {
      const err = error as { statusCode: number }
      if (err.statusCode === 401) {
        showError(t('toasts.errors.authRequired'))
        navigateTo('/')
        return
      }
      else if (err.statusCode === 403) {
        showError(t('toasts.errors.accessDenied'))
        return
      }
    }
    handleApiError(error)
  }

  return {
    handleApiError,
    handleValidationErrors,
    handleAuthError,
  }
}
