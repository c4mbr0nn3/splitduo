export default function useImportExport(groupId) {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const isImporting = ref(false)
  const isExporting = ref(false)

  // Import data backup
  const importData = async (file, importTypeId = 1) => {
    if (!groupIdRef.value) return

    isImporting.value = true
    try {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('importTypeId', importTypeId)

      const response = await api.post(
        `/groups/${groupIdRef.value}/imports`,
        formData,
        {
          headers: {
            'Content-Type': 'multipart/form-data',
          },
        },
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
    isImporting: readonly(isImporting),
    isExporting: readonly(isExporting),
    importData,
    exportToCsv,
    exportToCospend,
  }
}
