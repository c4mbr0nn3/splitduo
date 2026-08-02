import type { ImportStatus, ImportAnalysis, ImportMapping, Pagination } from '~/types/domain'

export default function useImportExport(groupId: string | Ref<string>) {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const imports = ref<ImportStatus[]>([])
  const pagination = ref<Pagination>({
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
  const analysisResults = ref<ImportAnalysis | null>(null)
  const currentImport = ref<ImportStatus | null>(null)

  // Helper function to safely parse JSON strings from backend
  const parseJsonField = <T>(jsonString: unknown, fieldName: string = 'JSON field'): T | null => {
    if (!jsonString) {
      return null
    }

    // If it's already an object, return as-is (for future backend changes)
    if (typeof jsonString === 'object') {
      return jsonString as T
    }

    // If it's a string, try to parse it
    if (typeof jsonString === 'string') {
      try {
        return JSON.parse(jsonString) as T
      }
      catch (error: unknown) {
        console.error(`Failed to parse ${fieldName}:`, error)
        showError(t('toasts.imports.invalidFormat', { fieldName }))
        return null
      }
    }

    return null
  }

  // Fetch imports with pagination
  const fetchImports = async (filters: { page?: number, limit?: number } = {}) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const params: Record<string, unknown> = {
        page: filters.page || 1,
        limit: filters.limit || 20,
      }

      const response = await api.getPaginated<ImportStatus>(
        `/groups/${groupIdRef.value}/imports`,
        params,
      )

      if (response.success) {
        // Parse JSON fields for each import
        const parsedImports = response.data.map(importItem => ({
          ...importItem,
          analysisResults: parseJsonField<ImportAnalysis>(importItem.analysisResults, 'analysis results'),
          mappingConfiguration: parseJsonField<Record<string, unknown>>(importItem.mappingConfiguration, 'mapping configuration'),
        })) as ImportStatus[]

        imports.value = parsedImports
        pagination.value = response.pagination || {
          page: 1, limit: 20, total: 0, totalPages: 0,
          hasNext: false, hasPrev: false,
        }
      }
    }
    catch (error: unknown) {
      showError(t('toasts.imports.loadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Phase 1: Analyze import file
  const analyzeFile = async (file: File, importTypeId: number = 1) => {
    if (!groupIdRef.value) return

    isAnalyzing.value = true
    try {
      const formData = new FormData()
      formData.append('File', file)
      formData.append('ImportTypeId', String(importTypeId))

      const response = await api.post<ImportStatus>(
        `/groups/${groupIdRef.value}/imports/analyze`,
        formData,
      )

      if (response.success && response.data) {
        currentImport.value = response.data
        // Parse the JSON string from backend to object
        analysisResults.value = parseJsonField<ImportAnalysis>(
          response.data.analysisResults,
          'analysis results',
        )

        showSuccess(t('toasts.imports.analyzed'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.imports.analyzeFailed'))
      throw error
    }
    finally {
      isAnalyzing.value = false
    }
  }

  // Phase 2: Import with mapping configuration
  const importWithMapping = async (mappingConfig: ImportMapping) => {
    if (!groupIdRef.value || !currentImport.value) return

    isImporting.value = true
    try {
      const requestData = {
        importId: currentImport.value.id,
        userMappings: mappingConfig.userMappings || {},
        aliasMappings: mappingConfig.aliasMappings || {},
        categoryMappings: mappingConfig.categoryMappings || {},
        paymentModeMappings: mappingConfig.paymentModeMappings || {},
      }

      const response = await api.post<ImportStatus>(
        `/groups/${groupIdRef.value}/imports`,
        requestData,
      )

      if (response.success && response.data) {
        showSuccess(t('toasts.imports.started'))
        // Clear state after successful import trigger
        analysisResults.value = null
        currentImport.value = null
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.imports.startFailed'))
      throw error
    }
    finally {
      isImporting.value = false
    }
  }

  // Legacy single-phase import (for backwards compatibility)
  const importData = async (file: File, importTypeId: number = 1) => {
    if (!groupIdRef.value) return

    isImporting.value = true
    try {
      const formData = new FormData()
      formData.append('File', file)
      formData.append('ImportTypeId', String(importTypeId))

      const response = await api.post<ImportStatus>(
        `/groups/${groupIdRef.value}/imports`,
        formData,
      )

      if (response.success && response.data) {
        showSuccess(t('toasts.imports.imported'))
        return response.data
      }
    }
    catch (error: unknown) {
      showError(t('toasts.imports.importFailed'))
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

  // Export to CSV — filename driven by backend Content-Disposition
  const exportToCsv = async () => {
    if (!groupIdRef.value) return

    isExporting.value = true
    try {
      const { blob, headers } = await api.getBlob(`/groups/${groupIdRef.value}/export/csv`)

      // Parse filename from Content-Disposition: attachment; filename="..."
      const disposition = headers.get('content-disposition') || ''
      const match = disposition.match(/filename="?([^";]+)"?/i)
      const fileName = match?.[1] || `export_${groupIdRef.value}.csv`

      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = fileName
      document.body.appendChild(a)
      a.click()
      window.URL.revokeObjectURL(url)
      document.body.removeChild(a)

      showSuccess(t('toasts.imports.exported'))
    }
    catch (error: unknown) {
      showError(t('toasts.imports.exportFailed'))
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
  }
}
