export function useNotifications() {
  const toast = useToast()

  const showSuccess = (message, title = 'Success') => {
    toast.add({
      title,
      description: message,
      color: 'green',
    })
  }

  const showError = (message, title = 'Error') => {
    toast.add({
      title,
      description: message,
      color: 'red',
    })
  }

  const showWarning = (message, title = 'Warning') => {
    toast.add({
      title,
      description: message,
      color: 'yellow',
    })
  }

  const showInfo = (message, title = 'Info') => {
    toast.add({
      title,
      description: message,
      color: 'blue',
    })
  }

  return {
    showSuccess,
    showError,
    showWarning,
    showInfo,
  }
}
