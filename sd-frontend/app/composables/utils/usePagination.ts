import type { Pagination } from '~/types/domain'

export default function usePagination() {
  const createPaginatedList = <T>(initialData: T[] = []) => {
    const items = ref<T[]>(initialData)
    const pagination = ref<Pagination>({
      page: 1,
      limit: 20,
      total: 0,
      totalPages: 0,
      hasNext: false,
      hasPrev: false,
    })
    const isLoading = ref(false)

    const nextPage = (): void => {
      if (pagination.value.hasNext) {
        pagination.value.page = Number(pagination.value.page) + 1
      }
    }

    const prevPage = (): void => {
      if (pagination.value.hasPrev) {
        pagination.value.page = Number(pagination.value.page) - 1
      }
    }

    const goToPage = (page: number): void => {
      const totalPages = Number(pagination.value.totalPages)
      if (page >= 1 && page <= totalPages) {
        pagination.value.page = page
      }
    }

    const setLimit = (limit: number): void => {
      pagination.value.limit = limit
      pagination.value.page = 1 // Reset to first page when changing limit
    }

    // Computed properties for easy access
    const currentPageItems = computed(() => items.value)
    const hasItems = computed(() => items.value.length > 0)
    const isEmpty = computed(() => !hasItems.value && !isLoading.value)
    const totalPages = computed(() => Number(pagination.value.totalPages))
    const currentPage = computed(() => Number(pagination.value.page))

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
