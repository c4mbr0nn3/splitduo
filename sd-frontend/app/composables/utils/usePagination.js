export default function usePagination() {
  const createPaginatedList = (initialData = []) => {
    const items = ref(initialData)
    const pagination = ref({
      page: 1,
      limit: 20,
      total: 0,
      totalPages: 0,
      hasNext: false,
      hasPrev: false,
    })
    const isLoading = ref(false)

    const nextPage = () => {
      if (pagination.value.hasNext) {
        pagination.value.page++
      }
    }

    const prevPage = () => {
      if (pagination.value.hasPrev) {
        pagination.value.page--
      }
    }

    const goToPage = (page) => {
      if (page >= 1 && page <= pagination.value.totalPages) {
        pagination.value.page = page
      }
    }

    const setLimit = (limit) => {
      pagination.value.limit = limit
      pagination.value.page = 1 // Reset to first page when changing limit
    }

    // Computed properties for easy access
    const currentPageItems = computed(() => items.value)
    const hasItems = computed(() => items.value.length > 0)
    const isEmpty = computed(() => !hasItems.value && !isLoading.value)
    const totalPages = computed(() => pagination.value.totalPages)
    const currentPage = computed(() => pagination.value.page)

    return {
      items,
      pagination: readonly(pagination),
      isLoading: readonly(isLoading),
      currentPageItems,
      hasItems,
      isEmpty,
      totalPages,
      currentPage,
      nextPage,
      prevPage,
      goToPage,
      setLimit,
    }
  }

  return {
    createPaginatedList,
  }
}
