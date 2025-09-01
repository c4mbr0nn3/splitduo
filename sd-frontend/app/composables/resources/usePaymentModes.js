export function usePaymentModes() {
  const api = useApi()
  const { showError } = useNotifications()

  const paymentModes = ref([])
  const isLoading = ref(false)

  // Get available payment modes
  const fetchPaymentModes = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/payment-modes')
      if (response.success && response.data) {
        paymentModes.value = response.data
      }
    }
    catch (error) {
      showError('Failed to load payment modes')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  return {
    paymentModes: readonly(paymentModes),
    isLoading: readonly(isLoading),
    fetchPaymentModes,
  }
}
