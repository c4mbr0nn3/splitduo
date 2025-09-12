// Global state for payment modes (singleton)
const globalPaymentModes = ref([])
const globalIsLoading = ref(false)
const globalIsInitialized = ref(false)
let fetchPromise = null

export default function usePaymentModes() {
  const api = useApi()
  const { showError } = useNotifications()

  // Get available payment modes (singleton with auto-initialization)
  const fetchPaymentModes = async () => {
    // If already initialized, return immediately
    if (globalIsInitialized.value) {
      return
    }

    // If already fetching, return the existing promise
    if (fetchPromise) {
      return fetchPromise
    }

    globalIsLoading.value = true
    fetchPromise = (async () => {
      try {
        const response = await api.get('/payment-modes')
        if (response.success && response.data) {
          globalPaymentModes.value = response.data
          globalIsInitialized.value = true
        }
      }
      catch (error) {
        showError('Failed to load payment modes')
        throw error
      }
      finally {
        globalIsLoading.value = false
        fetchPromise = null
      }
    })()

    return fetchPromise
  }

  // Auto-initialize on first use
  if (!globalIsInitialized.value && !fetchPromise) {
    fetchPaymentModes()
  }

  // Helper function to get payment mode name by ID
  const getPaymentModeName = (paymentModeId) => {
    const paymentMode = globalPaymentModes.value.find(pm => pm.id === paymentModeId)
    return paymentMode ? paymentMode.name : 'Unknown'
  }

  return {
    paymentModes: readonly(globalPaymentModes),
    isLoading: readonly(globalIsLoading),
    fetchPaymentModes,
    getPaymentModeName,
  }
}
