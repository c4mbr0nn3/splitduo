import { useDebounceFn } from '@vueuse/core'

/**
 * Composable for debounced search functionality
 */
export default function useDebounceSearch(delay: number = 300) {
  const searchQuery = ref('')
  const debouncedSearchQuery = ref('')

  // Debounced search function
  const debouncedSearch = useDebounceFn((query: string) => {
    debouncedSearchQuery.value = query
  }, delay)

  // Watch for searchQuery changes and trigger debounced search
  watch(searchQuery, (newQuery: string) => {
    debouncedSearch(newQuery)
  }, { immediate: true })

  // Clear search
  const clearSearch = (): void => {
    searchQuery.value = ''
    debouncedSearchQuery.value = ''
  }

  // Set search query programmatically
  const setSearchQuery = (query: string): void => {
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
