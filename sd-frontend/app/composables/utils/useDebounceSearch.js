import { useDebounceFn } from '@vueuse/core'

/**
 * Composable for debounced search functionality
 * @param {number} delay - Debounce delay in milliseconds (default: 300)
 * @returns {Object} - Search state and utilities
 */
export default function useDebounceSearch(delay = 300) {
  const searchQuery = ref('')
  const debouncedSearchQuery = ref('')

  // Debounced search function
  const debouncedSearch = useDebounceFn((query) => {
    debouncedSearchQuery.value = query
  }, delay)

  // Watch for searchQuery changes and trigger debounced search
  watch(searchQuery, (newQuery) => {
    debouncedSearch(newQuery)
  }, { immediate: true })

  // Clear search
  const clearSearch = () => {
    searchQuery.value = ''
    debouncedSearchQuery.value = ''
  }

  // Set search query programmatically
  const setSearchQuery = (query) => {
    searchQuery.value = query
  }

  return {
    // Reactive refs
    searchQuery: readonly(searchQuery),
    debouncedSearchQuery: readonly(debouncedSearchQuery),

    // Utilities
    clearSearch,
    setSearchQuery,

    // For v-model binding (writable)
    searchInput: searchQuery,
  }
}
