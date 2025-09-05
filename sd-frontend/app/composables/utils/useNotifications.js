export default function useNotifications() {
  const toast = useToast()

  const showSuccess = (message, title = 'Success') => {
    toast.add({
      title,
      description: message,
    })
  }

  const showError = (message, title = 'Error') => {
    toast.add({
      title,
      description: message,
    })
  }

  const showWarning = (message, title = 'Warning') => {
    toast.add({
      title,
      description: message,
    })
  }

  const showInfo = (message, title = 'Info') => {
    toast.add({
      title,
      description: message,
    })
  }

  return {
    showSuccess,
    showError,
    showWarning,
    showInfo,
  }
}
