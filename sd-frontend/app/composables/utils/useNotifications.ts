export default function useNotifications() {
  const toast = useToast()

  const showSuccess = (message: string, title: string = 'Success'): void => {
    toast.add({
      title,
      description: message,
      color: 'success',
      duration: 4000,
      position: 'top-center',
    } as Parameters<typeof toast.add>[0])
  }

  const showError = (message: string, title: string = 'Error'): void => {
    toast.add({
      title,
      description: message,
      color: 'error',
      duration: 4000,
      position: 'top-center',
    } as Parameters<typeof toast.add>[0])
  }

  const showWarning = (message: string, title: string = 'Warning'): void => {
    toast.add({
      title,
      description: message,
      color: 'warning',
      duration: 4000,
      position: 'top-center',
    } as Parameters<typeof toast.add>[0])
  }

  const showInfo = (message: string, title: string = 'Info'): void => {
    toast.add({
      title,
      description: message,
      color: 'info',
      duration: 4000,
      position: 'top-center',
    } as Parameters<typeof toast.add>[0])
  }

  return {
    showSuccess,
    showError,
    showWarning,
    showInfo,
  }
}
