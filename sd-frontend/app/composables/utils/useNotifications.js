export default function useNotifications() {
  const toast = useToast()

  const showSuccess = (message, title = 'Success') => {
    toast.add({
      title,
      description: message,
      color: 'success',
    })
  }

  const showError = (message, title = 'Error') => {
    toast.add({
      title,
      description: message,
      color: 'error',
    })
  }

  const showWarning = (message, title = 'Warning') => {
    toast.add({
      title,
      description: message,
      color: 'warning',
    })
  }

  const showInfo = (message, title = 'Info') => {
    toast.add({
      title,
      description: message,
      color: 'info',
    })
  }

  return {
    showSuccess,
    showError,
    showWarning,
    showInfo,
  }
}
