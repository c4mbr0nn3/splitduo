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
  // Two-phase import state
  const isAnalyzing = ref(false)
  const analysisResults = ref(null)
  const currentImport = ref(null)

  // Helper function to safely parse JSON strings from backend
  const parseJsonField = (jsonString, fieldName = 'JSON field') => {
    if (!jsonString) {
      return null
    }

    // If it's already an object, return as-is (for future backend changes)
    if (typeof jsonString === 'object') {
      return jsonString
    }

    // If it's a string, try to parse it
    if (typeof jsonString === 'string') {
      try {
        return JSON.parse(jsonString)
      }
      catch (error) {
        console.error(`Failed to parse ${fieldName}:`, error)
        showError(`Invalid ${fieldName} format received from server`)
        return null
      }
    }

    return null
  }

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
        // Parse JSON fields for each import
        const parsedImports = response.data.map(importItem => ({
          ...importItem,
          analysisResults: parseJsonField(importItem.analysisResults, 'analysis results'),
          mappingConfiguration: parseJsonField(importItem.mappingConfiguration, 'mapping configuration'),
        }))

        imports.value = parsedImports
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

  // Phase 1: Analyze import file
  const analyzeFile = async (file, importTypeId = 1) => {
    if (!groupIdRef.value) return

    isAnalyzing.value = true
    try {
      const formData = new FormData()
      formData.append('File', file)
      formData.append('ImportTypeId', importTypeId)

      const response = await api.post(
        `/groups/${groupIdRef.value}/imports/analyze`,
        formData,
      )

      if (response.success && response.data) {
        currentImport.value = response.data
        // Parse the JSON string from backend to object
        analysisResults.value = parseJsonField(
          response.data.analysisResults,
          'analysis results',
        )

        showSuccess('File analyzed successfully')
        return response.data
      }
    }
    catch (error) {
      showError('Failed to analyze file')
      throw error
    }
    finally {
      isAnalyzing.value = false
    }
  }

  // Phase 2: Import with mapping configuration
  const importWithMapping = async (mappingConfig) => {
    if (!groupIdRef.value || !currentImport.value) return

    isImporting.value = true
    try {
      const requestData = {
        importId: currentImport.value.id,
        userMappings: mappingConfig.userMappings || {},
        categoryMappings: mappingConfig.categoryMappings || {},
        paymentModeMappings: mappingConfig.paymentModeMappings || {},
      }

      const response = await api.post(
        `/groups/${groupIdRef.value}/imports`,
        requestData,
      )

      if (response.success && response.data) {
        showSuccess('Import started successfully')
        // Clear state after successful import trigger
        analysisResults.value = null
        currentImport.value = null
        return response.data
      }
    }
    catch (error) {
      showError('Failed to start import')
      throw error
    }
    finally {
      isImporting.value = false
    }
  }

  // Legacy single-phase import (for backwards compatibility)
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

  // Clear analysis state
  const clearAnalysis = () => {
    analysisResults.value = null
    currentImport.value = null
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
    // Two-phase import state and methods
    isAnalyzing: readonly(isAnalyzing),
    analysisResults: readonly(analysisResults),
    currentImport: readonly(currentImport),
    analyzeFile,
    importWithMapping,
    clearAnalysis,
    // Legacy methods
    fetchImports,
    importData,
    exportToCsv,
    exportToCospend,
  }
}
