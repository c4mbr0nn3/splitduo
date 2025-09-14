export default function useImportExport(groupId) {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const imports = ref([])
  const pagination = ref({
    page: 1,
    limit: 20,
    total: 0,
    totalPages: 0,
    hasNext: false,
    hasPrev: false,
  })
  const isImporting = ref(false)
  const isExporting = ref(false)
  const isLoading = ref(false)

  // Fetch imports with pagination
  const fetchImports = async (filters = {}) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const params = {
        page: filters.page || 1,
        limit: filters.limit || 20,
      }

      const response = await api.get(
        `/groups/${groupIdRef.value}/imports`,
        params,
      )

      if (response.success && response.data) {
        imports.value = response.data
        pagination.value = response.pagination || {
          page: 1, limit: 20, total: 0, totalPages: 0,
          hasNext: false, hasPrev: false,
        }
      }
    }
    catch (error) {
      showError('Failed to load imports')
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Import data backup
  const importData = async (file, importTypeId = 1) => {
    if (!groupIdRef.value) return

    isImporting.value = true
    try {
      const formData = new FormData()
      formData.append('File', file)
      formData.append('ImportTypeId', importTypeId)

      const response = await api.post(
        `/groups/${groupIdRef.value}/imports`,
        formData,
      )

      if (response.success && response.data) {
        showSuccess('Data imported successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to import data')
      throw error
    }
    finally {
      isImporting.value = false
    }
  }

  // Export to CSV
  const exportToCsv = async () => {
    if (!groupIdRef.value) return

    isExporting.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/export/csv`)

      // Handle file download
      const blob = new Blob([response], { type: 'text/csv' })
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `splitduo-export-${groupIdRef.value}.csv`
      document.body.appendChild(a)
      a.click()
      window.URL.revokeObjectURL(url)
      document.body.removeChild(a)

      showSuccess('Data exported successfully')
    }
    catch (error) {
      showError('Failed to export data')
      throw error
    }
    finally {
      isExporting.value = false
    }
  }

  // Export to Cospend format
  const exportToCospend = async () => {
    if (!groupIdRef.value) return

    isExporting.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/export/cospend`)

      // Handle file download
      const blob = new Blob([response], { type: 'application/json' })
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `cospend-export-${groupIdRef.value}.json`
      document.body.appendChild(a)
      a.click()
      window.URL.revokeObjectURL(url)
      document.body.removeChild(a)

      showSuccess('Cospend export successful')
    }
    catch (error) {
      showError('Failed to export to Cospend format')
      throw error
    }
    finally {
      isExporting.value = false
    }
  }

  return {
    imports: readonly(imports),
    pagination: readonly(pagination),
    isImporting: readonly(isImporting),
    isExporting: readonly(isExporting),
    isLoading: readonly(isLoading),
    fetchImports,
    importData,
    exportToCsv,
    exportToCospend,
  }
}
