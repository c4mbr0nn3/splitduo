export default function useNotifications() {
  const toast = useToast()

  const showSuccess = (message, title = 'Success') => {
    toast.add({
      title,
      description: message,
      color: 'success',
      duration: 4000,
      position: 'top-center',
    })
  }

  const showError = (message, title = 'Error') => {
    toast.add({
      title,
      description: message,
      color: 'error',
      duration: 4000,
      position: 'top-center',
    })
  }

  const showWarning = (message, title = 'Warning') => {
    toast.add({
      title,
      description: message,
      color: 'warning',
      duration: 4000,
      position: 'top-center',
    })
  }

  const showInfo = (message, title = 'Info') => {
    toast.add({
      title,
      description: message,
      color: 'info',
      duration: 4000,
      position: 'top-center',
    })
  }

  return {
    showSuccess,
    showError,
    showWarning,
    showInfo,
  }
}
