import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref } from 'vue'

import useImportExport from './useImportExport'
import { apiMock } from '~/composables/api/base.mock'
import type { ImportAnalysis, ImportStatus, Pagination } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useImportExport.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const analysis: ImportAnalysis = {
  fileHash: 'abc123',
  members: [{ key: 'alice@example.com', value: 'Alice' }],
  categories: [{ key: '1', value: 'Food' }],
  paymentModes: [{ key: '1', value: 'Cash' }],
  aliases: [],
}

const importStatus = (overrides: Partial<ImportStatus> = {}): ImportStatus => ({
  id: 'import-1',
  fileName: 'expenses.csv',
  fileHash: 'abc123',
  importStatusId: 1,
  importTypeId: 1,
  recordsCount: 10,
  errorDetails: '',
  importDate: '2026-01-01',
  createdAt: 0,
  updatedAt: 0,
  ...overrides,
})

const pagination: Pagination = {
  page: 1,
  limit: 20,
  total: 1,
  totalPages: 1,
  hasNext: false,
  hasPrev: false,
}

const csvFile = (): File => new File(['content'], 'expenses.csv', { type: 'text/csv' })

describe('useImportExport', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchImports', () => {
    it('stores the imports and pagination from the response', async () => {
      const responsePagination = { page: 2, limit: 20, total: 25, totalPages: 2, hasNext: true, hasPrev: true }
      apiMock.getPaginated.mockResolvedValue({
        success: true,
        data: [importStatus({ id: 'import-1' })],
        pagination: responsePagination,
      })
      const ie = useImportExport('group-1')

      await ie.fetchImports({ page: 2, limit: 20 })

      expect(apiMock.getPaginated).toHaveBeenCalledWith('/groups/group-1/imports', { page: 2, limit: 20 })
      expect(ie.imports.value).toEqual([
        { ...importStatus({ id: 'import-1' }), analysisResults: null, mappingConfiguration: null },
      ])
      expect(ie.pagination.value).toEqual(responsePagination)
    })

    it('parses analysisResults and mappingConfiguration JSON strings on each import', async () => {
      const raw = importStatus({
        id: 'import-1',
        analysisResults: JSON.stringify(analysis),
        mappingConfiguration: JSON.stringify({ categoryMappings: { 1: 2 } }),
      })
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [raw], pagination })
      const ie = useImportExport('group-1')

      await ie.fetchImports()

      expect(ie.imports.value[0]?.analysisResults).toEqual(analysis)
      expect(ie.imports.value[0]?.mappingConfiguration).toEqual({ categoryMappings: { 1: 2 } })
    })

    it('shows an error toast and stores null when analysisResults is invalid JSON', async () => {
      const raw = importStatus({ id: 'import-1', analysisResults: '{not valid json' })
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [raw], pagination })
      const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
      const ie = useImportExport('group-1')

      await ie.fetchImports()

      expect(tMock).toHaveBeenCalledWith('toasts.imports.invalidFormat', { fieldName: 'analysis results' })
      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.imports.invalidFormat')
      expect(ie.imports.value[0]?.analysisResults).toBeNull()
      errorSpy.mockRestore()
    })

    it('uses analysisResults as-is when the backend already returns an object', async () => {
      const raw = { ...importStatus({ id: 'import-1' }), analysisResults: analysis }
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [raw], pagination })
      const ie = useImportExport('group-1')

      await ie.fetchImports()

      expect(ie.imports.value[0]?.analysisResults).toEqual(analysis)
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const ie = useImportExport('')

      await ie.fetchImports()

      expect(apiMock.getPaginated).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.getPaginated.mockRejectedValue(new Error('Network down'))
      const ie = useImportExport('group-1')

      await expect(ie.fetchImports()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.imports.loadFailed')
      expect(ie.isLoading.value).toBe(false)
    })

    it('uses the current value of a reactive groupId', async () => {
      apiMock.getPaginated.mockResolvedValue({ success: true, data: [], pagination })
      const groupId = ref('group-1')
      const ie = useImportExport(groupId)

      await ie.fetchImports()
      expect(apiMock.getPaginated).toHaveBeenCalledWith('/groups/group-1/imports', { page: 1, limit: 20 })

      groupId.value = 'group-2'
      await ie.fetchImports()
      expect(apiMock.getPaginated).toHaveBeenLastCalledWith('/groups/group-2/imports', { page: 1, limit: 20 })
    })
  })

  describe('analyzeFile', () => {
    it('stores the import and parsed analysis results, shows a success toast, and returns the import', async () => {
      const raw = importStatus({ id: 'import-1', analysisResults: JSON.stringify(analysis) })
      apiMock.post.mockResolvedValue({ success: true, data: raw })
      const ie = useImportExport('group-1')
      const file = csvFile()

      const result = await ie.analyzeFile(file, 1)

      expect(apiMock.post).toHaveBeenCalledWith('/groups/group-1/imports/analyze', expect.any(FormData))
      const formData = apiMock.post.mock.calls[0]?.[1] as FormData
      expect(formData.get('File')).toBe(file)
      expect(formData.get('ImportTypeId')).toBe('1')
      expect(ie.currentImport.value).toEqual(raw)
      expect(ie.analysisResults.value).toEqual(analysis)
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.imports.analyzed')
      expect(result).toEqual(raw)
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const ie = useImportExport('')

      await ie.analyzeFile(csvFile())

      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isAnalyzing when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Analyze failed'))
      const ie = useImportExport('group-1')

      await expect(ie.analyzeFile(csvFile())).rejects.toThrow('Analyze failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.imports.analyzeFailed')
      expect(ie.isAnalyzing.value).toBe(false)
    })

    it('sets isAnalyzing during the call and clears it afterwards', async () => {
      let resolveAnalyze: (value: unknown) => void = () => {}
      apiMock.post.mockImplementation(() => new Promise((resolve) => {
        resolveAnalyze = resolve
      }))
      const ie = useImportExport('group-1')

      const pending = ie.analyzeFile(csvFile())
      expect(ie.isAnalyzing.value).toBe(true)

      resolveAnalyze({ success: true, data: importStatus({ id: 'import-1' }) })
      await pending

      expect(ie.isAnalyzing.value).toBe(false)
    })
  })

  describe('importWithMapping', () => {
    it('sets isImporting during the call, posts the mapping payload, clears analysis state, and returns the import', async () => {
      let resolveImport: (value: unknown) => void = () => {}
      apiMock.post
        .mockResolvedValueOnce({ success: true, data: importStatus({ id: 'import-1', analysisResults: JSON.stringify(analysis) }) })
        .mockImplementationOnce(() => new Promise((resolve) => {
          resolveImport = resolve
        }))
      const ie = useImportExport('group-1')
      await ie.analyzeFile(csvFile())

      const pending = ie.importWithMapping({
        userMappings: { 'alice@example.com': 'user-1' },
        categoryMappings: { 1: 2 },
        paymentModeMappings: { 1: 1 },
      })
      expect(ie.isImporting.value).toBe(true)

      resolveImport({ success: true, data: importStatus({ id: 'import-1' }) })
      const result = await pending

      expect(apiMock.post).toHaveBeenLastCalledWith('/groups/group-1/imports', {
        importId: 'import-1',
        userMappings: { 'alice@example.com': 'user-1' },
        aliasMappings: {},
        categoryMappings: { 1: 2 },
        paymentModeMappings: { 1: 1 },
      })
      expect(ie.isImporting.value).toBe(false)
      expect(ie.analysisResults.value).toBeNull()
      expect(ie.currentImport.value).toBeNull()
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.imports.started')
      expect(result).toEqual(importStatus({ id: 'import-1' }))
    })

    it('returns early without calling the API when no import has been analyzed', async () => {
      const ie = useImportExport('group-1')

      await ie.importWithMapping({ userMappings: {} })

      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const ie = useImportExport('')

      await ie.importWithMapping({ userMappings: {} })

      expect(apiMock.post).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isImporting when the API call fails', async () => {
      apiMock.post
        .mockResolvedValueOnce({ success: true, data: importStatus({ id: 'import-1' }) })
        .mockRejectedValueOnce(new Error('Start failed'))
      const ie = useImportExport('group-1')
      await ie.analyzeFile(csvFile())

      await expect(ie.importWithMapping({ userMappings: {} })).rejects.toThrow('Start failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.imports.startFailed')
      expect(ie.isImporting.value).toBe(false)
    })

    it('includes aliasMappings in the payload for alias-mode groups', async () => {
      apiMock.post
        .mockResolvedValueOnce({ success: true, data: importStatus({ id: 'import-1' }) })
        .mockResolvedValueOnce({ success: true, data: importStatus({ id: 'import-1' }) })
      const ie = useImportExport('group-1')
      await ie.analyzeFile(csvFile())

      await ie.importWithMapping({ aliasMappings: { 'Family Dinner': 'alias-1' } })

      expect(apiMock.post).toHaveBeenLastCalledWith('/groups/group-1/imports', {
        importId: 'import-1',
        userMappings: {},
        aliasMappings: { 'Family Dinner': 'alias-1' },
        categoryMappings: {},
        paymentModeMappings: {},
      })
    })
  })

  describe('clearAnalysis', () => {
    it('clears analysisResults and currentImport', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: importStatus({ id: 'import-1', analysisResults: JSON.stringify(analysis) }) })
      const ie = useImportExport('group-1')
      await ie.analyzeFile(csvFile())
      expect(ie.analysisResults.value).not.toBeNull()
      expect(ie.currentImport.value).not.toBeNull()

      ie.clearAnalysis()

      expect(ie.analysisResults.value).toBeNull()
      expect(ie.currentImport.value).toBeNull()
    })
  })

  describe('exportToCsv', () => {
    it('downloads the blob with the filename from Content-Disposition and shows a success toast', async () => {
      const blob = new Blob(['a,b,c'], { type: 'text/csv' })
      const headers = new Headers({ 'content-disposition': 'attachment; filename="expenses_2026.csv"' })
      apiMock.getBlob.mockResolvedValue({ blob, headers })
      const createObjectURLSpy = vi.spyOn(window.URL, 'createObjectURL').mockReturnValue('blob:mock-url')
      const revokeObjectURLSpy = vi.spyOn(window.URL, 'revokeObjectURL').mockImplementation(() => {})
      const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
      const appendChildSpy = vi.spyOn(document.body, 'appendChild')
      const removeChildSpy = vi.spyOn(document.body, 'removeChild')
      const ie = useImportExport('group-1')

      await ie.exportToCsv()

      expect(apiMock.getBlob).toHaveBeenCalledWith('/groups/group-1/export/csv')
      expect(createObjectURLSpy).toHaveBeenCalledWith(blob)
      expect(clickSpy).toHaveBeenCalledTimes(1)
      expect(revokeObjectURLSpy).toHaveBeenCalledWith('blob:mock-url')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.imports.exported')
      const anchor = appendChildSpy.mock.calls[0]?.[0] as HTMLAnchorElement
      expect(anchor.download).toBe('expenses_2026.csv')
      expect(anchor.href).toBe('blob:mock-url')
      expect(removeChildSpy).toHaveBeenCalledWith(anchor)
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const ie = useImportExport('')

      await ie.exportToCsv()

      expect(apiMock.getBlob).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isExporting when the API call fails', async () => {
      apiMock.getBlob.mockRejectedValue(new Error('Export failed'))
      const ie = useImportExport('group-1')

      await expect(ie.exportToCsv()).rejects.toThrow('Export failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.imports.exportFailed')
      expect(ie.isExporting.value).toBe(false)
    })

    it('falls back to export_<groupId>.csv when Content-Disposition is absent', async () => {
      apiMock.getBlob.mockResolvedValue({ blob: new Blob(['a,b,c']), headers: new Headers() })
      const createObjectURLSpy = vi.spyOn(window.URL, 'createObjectURL').mockReturnValue('blob:mock-url')
      const revokeObjectURLSpy = vi.spyOn(window.URL, 'revokeObjectURL').mockImplementation(() => {})
      const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
      const appendChildSpy = vi.spyOn(document.body, 'appendChild')
      const ie = useImportExport('group-1')

      await ie.exportToCsv()

      const anchor = appendChildSpy.mock.calls[0]?.[0] as HTMLAnchorElement
      expect(anchor.download).toBe('export_group-1.csv')
      expect(clickSpy).toHaveBeenCalledTimes(1)
      expect(createObjectURLSpy).toHaveBeenCalledTimes(1)
      expect(revokeObjectURLSpy).toHaveBeenCalledTimes(1)
    })
  })

  describe('two-phase import state machine', () => {
    it('advances from idle to analyzed after analyzeFile', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: importStatus({ id: 'import-1', analysisResults: JSON.stringify(analysis) }) })
      const ie = useImportExport('group-1')

      expect(ie.analysisResults.value).toBeNull()
      expect(ie.currentImport.value).toBeNull()

      await ie.analyzeFile(csvFile())

      expect(ie.currentImport.value?.id).toBe('import-1')
      expect(ie.analysisResults.value).toEqual(analysis)
    })

    it('advances from analyzed to done after importWithMapping', async () => {
      apiMock.post
        .mockResolvedValueOnce({ success: true, data: importStatus({ id: 'import-1', analysisResults: JSON.stringify(analysis) }) })
        .mockResolvedValueOnce({ success: true, data: importStatus({ id: 'import-1' }) })
      const ie = useImportExport('group-1')
      await ie.analyzeFile(csvFile())
      expect(ie.analysisResults.value).toEqual(analysis)

      await ie.importWithMapping({ userMappings: {} })

      expect(ie.analysisResults.value).toBeNull()
      expect(ie.currentImport.value).toBeNull()
    })

    it('returns to idle when clearAnalysis is called after analyzeFile', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: importStatus({ id: 'import-1', analysisResults: JSON.stringify(analysis) }) })
      const ie = useImportExport('group-1')
      await ie.analyzeFile(csvFile())
      expect(ie.analysisResults.value).toEqual(analysis)

      ie.clearAnalysis()

      expect(ie.analysisResults.value).toBeNull()
      expect(ie.currentImport.value).toBeNull()
    })
  })
})
