export function useErrorHandling() {
  const { showError } = useNotifications()

  const handleApiError = (error, defaultMessage = 'An error occurred') => {
    const message = error?.data?.error?.message
      || error?.message
      || defaultMessage

    showError(message)
  }

  const handleValidationErrors = (errors) => {
    if (Array.isArray(errors)) {
      errors.forEach((error) => {
        showError(`${error.field}: ${error.message}`)
      })
    }
  }

  const handleAuthError = (error) => {
    if (error.statusCode === 401) {
      showError('Authentication required. Please log in.')
      navigateTo('/login')
    }
    else if (error.statusCode === 403) {
      showError('Access denied. You do not have permission.')
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
