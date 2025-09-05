export default function useCategories() {
  const api = useApi()
  const { showError } = useNotifications()

  const categories = ref([])
  const isLoading = ref(false)

  // Get available categories
  const fetchCategories = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/categories')
      if (response.success && response.data) {
        categories.value = response.data
      }
    }
    catch (error) {
      showError('Failed to load categories')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  return {
    categories: readonly(categories),
    isLoading: readonly(isLoading),
    fetchCategories,
  }
}
