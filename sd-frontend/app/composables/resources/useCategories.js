// Global state for categories (singleton)
const globalCategories = ref([])
const globalIsLoading = ref(false)
const globalIsInitialized = ref(false)
let fetchPromise = null

export default function useCategories() {
  const api = useApi()
  const { t } = useI18n()
  const { showError } = useNotifications()

  // Get available categories (singleton with auto-initialization)
  const fetchCategories = async () => {
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
        const response = await api.get('/categories')
        if (response.success && response.data) {
          globalCategories.value = response.data
          globalIsInitialized.value = true
        }
      }
      catch (error) {
        showError(t('toasts.categories.loadFailed'))
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
    fetchCategories()
  }

  // Helper function to get category name by ID
  const getCategoryName = (categoryId) => {
    const category = globalCategories.value.find(cat => cat.id === categoryId)
    return category ? category.name : 'Unknown'
  }

  return {
    categories: readonly(globalCategories),
    isLoading: readonly(globalIsLoading),
    fetchCategories,
    getCategoryName,
  }
}
