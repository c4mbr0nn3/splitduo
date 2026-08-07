import { describe, it, expect } from 'vitest'

import usePagination from './usePagination'

describe('usePagination', () => {
  describe('initial state', () => {
    it('creates an empty list with default pagination when called without arguments', () => {
      const list = usePagination().createPaginatedList()

      expect(list.items.value).toEqual([])
      expect(list.pagination.value).toEqual({
        page: 1,
        limit: 20,
        total: 0,
        totalPages: 0,
        hasNext: false,
        hasPrev: false,
      })
      expect(list.isLoading.value).toBe(false)
    })

    it('seeds items from the initial data argument', () => {
      const list = usePagination().createPaginatedList([1, 2, 3])

      expect(list.items.value).toEqual([1, 2, 3])
      expect(list.hasItems.value).toBe(true)
      expect(list.isEmpty.value).toBe(false)
    })

    it('exposes computed properties that reflect the current state', () => {
      const list = usePagination().createPaginatedList([1, 2, 3])
      list.setPagination({ page: 3, totalPages: 5 })

      expect(list.currentPageItems.value).toEqual([1, 2, 3])
      expect(list.hasItems.value).toBe(true)
      expect(list.isEmpty.value).toBe(false)
      expect(list.totalPages.value).toBe(5)
      expect(list.currentPage.value).toBe(3)
    })
  })

  describe('nextPage', () => {
    it('advances the page when hasNext is true', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 2, totalPages: 5, hasNext: true })

      list.nextPage()

      expect(list.currentPage.value).toBe(3)
    })

    it('does nothing when hasNext is false', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 2, totalPages: 5, hasNext: false })

      list.nextPage()

      expect(list.currentPage.value).toBe(2)
    })

    it('advances the page by exactly 1', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 1, totalPages: 5, hasNext: true })

      list.nextPage()

      expect(list.currentPage.value).toBe(2)
    })
  })

  describe('prevPage', () => {
    it('decrements the page when hasPrev is true', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 3, totalPages: 5, hasPrev: true })

      list.prevPage()

      expect(list.currentPage.value).toBe(2)
    })

    it('does nothing when hasPrev is false', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 3, totalPages: 5, hasPrev: false })

      list.prevPage()

      expect(list.currentPage.value).toBe(3)
    })
  })

  describe('goToPage', () => {
    it('navigates to a valid page within [1, totalPages]', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ totalPages: 5 })

      list.goToPage(4)

      expect(list.currentPage.value).toBe(4)
    })

    it('does nothing for a page below 1', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 2, totalPages: 5 })

      list.goToPage(-1)

      expect(list.currentPage.value).toBe(2)
    })

    it('does nothing for a page above totalPages', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 2, totalPages: 5 })

      list.goToPage(6)

      expect(list.currentPage.value).toBe(2)
    })

    it('does nothing for page 0', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 2, totalPages: 5 })

      list.goToPage(0)

      expect(list.currentPage.value).toBe(2)
    })
  })

  describe('setLimit', () => {
    it('changes the limit and keeps the page at 1', () => {
      const list = usePagination().createPaginatedList()

      list.setLimit(50)

      expect(list.pagination.value.limit).toBe(50)
      expect(list.currentPage.value).toBe(1)
    })

    it('resets the page to 1 when currently on a different page', () => {
      const list = usePagination().createPaginatedList()
      list.setPagination({ page: 4, limit: 10 })

      list.setLimit(25)

      expect(list.pagination.value.limit).toBe(25)
      expect(list.currentPage.value).toBe(1)
    })
  })

  describe('edge cases', () => {
    it('reports an empty list as empty when not loading', () => {
      const list = usePagination().createPaginatedList()

      expect(list.hasItems.value).toBe(false)
      expect(list.isEmpty.value).toBe(true)
    })
  })
})
