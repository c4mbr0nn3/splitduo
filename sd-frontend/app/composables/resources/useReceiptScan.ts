import type { ParsedReceipt } from '~/types/domain'

export type ScanPhase = 'idle' | 'preparing' | 'analyzing' | 'success' | 'error'

// Module-scoped singleton state: useReceiptScan() is called separately by the
// scan button and the scan modal, and per-instance refs would NOT share.
const receiptImageUrl = ref<string | null>(null)
const scanPhase = ref<ScanPhase>('idle')
const isScanning = ref(false)
const scanError = ref<string | null>(null)
const lastScanFile = ref<File | null>(null)
const lastScanGroupId = ref<string | null>(null)
let abortController: AbortController | null = null

// Brief dwell on the success phase so the confirmation overlay is perceivable
// before navigation replaces the modal.
const SUCCESS_HOLD_MS = 650

const isAbort = (e: unknown): boolean =>
  (e instanceof DOMException && e.name === 'AbortError')
  || (e instanceof Error && e.name === 'AbortError')
  || ((e as { cause?: unknown })?.cause instanceof DOMException && ((e as { cause: DOMException }).cause).name === 'AbortError')
  || (e as { cause?: { name?: string } })?.cause?.name === 'AbortError'

export default function useReceiptScan() {
  const api = useApi()
  const router = useRouter()
  const { t } = useI18n()

  const clearReceiptImage = () => {
    if (receiptImageUrl.value) URL.revokeObjectURL(receiptImageUrl.value)
    receiptImageUrl.value = null
  }

  const compressImage = (file: File): Promise<Blob | null> => {
    return new Promise((resolve) => {
      const img = new Image()
      img.onload = () => {
        const max = 2000
        let { width, height } = img
        if (width > max || height > max) {
          const ratio = Math.min(max / width, max / height)
          width = Math.round(width * ratio)
          height = Math.round(height * ratio)
        }
        const canvas = document.createElement('canvas')
        canvas.width = width
        canvas.height = height
        const ctx = canvas.getContext('2d')
        if (ctx) {
          ctx.drawImage(img, 0, 0, width, height)
        }
        URL.revokeObjectURL(img.src)
        canvas.toBlob((blob) => {
          resolve(blob)
        }, 'image/jpeg', 0.8)
      }
      img.onerror = () => {
        URL.revokeObjectURL(img.src)
        resolve(null)
      }
      img.src = URL.createObjectURL(file)
    })
  }

  const scanReceipt = async (file: File, groupId: string | null = null) => {
    if (isScanning.value) return
    abortController?.abort()
    abortController = new AbortController()
    const { signal } = abortController
    lastScanFile.value = file
    lastScanGroupId.value = groupId
    scanError.value = null
    isScanning.value = true
    scanPhase.value = 'preparing'
    try {
      const compressed = await compressImage(file)
      if (signal.aborted) return
      if (!compressed) {
        scanError.value = t('toasts.receipts.scanFailed')
        scanPhase.value = 'error'
        return
      }
      if (receiptImageUrl.value) URL.revokeObjectURL(receiptImageUrl.value)
      receiptImageUrl.value = URL.createObjectURL(compressed)
      const form = new FormData()
      form.append('image', compressed, 'receipt.jpg')

      scanPhase.value = 'analyzing'
      const response = await api.post<ParsedReceipt>('/receipts/parse', form, { signal })
      if (!response.success) throw new Error(response.error?.message || 'Scan failed')

      scanPhase.value = 'success'
      // Brief, honest dwell so the success confirmation is perceivable before the
      // navigation replaces the modal.
      await new Promise(resolve => setTimeout(resolve, SUCCESS_HOLD_MS))
      if (signal.aborted) return
      const { title, amount, description, expenseDate, categoryId, paymentModeId } = response.data as ParsedReceipt
      const query: Record<string, string | number> = {}
      if (groupId) query.groupId = groupId
      if (title) query.title = title
      if (amount != null) query.amount = amount
      if (description) query.description = description
      if (expenseDate) query.expenseDate = expenseDate
      if (categoryId != null) query.categoryId = categoryId
      if (paymentModeId != null) query.paymentModeId = paymentModeId

      await router.push({ path: '/expenses/add', query })
    }
    catch (error) {
      if (isAbort(error) || signal.aborted) {
        scanPhase.value = 'idle'
        scanError.value = null
        return
      }
      scanError.value = t('toasts.receipts.scanFailed')
      scanPhase.value = 'error'
    }
    finally {
      isScanning.value = false
      if (scanPhase.value !== 'success') abortController = null
    }
  }

  const cancelScan = () => {
    abortController?.abort()
    if (!isScanning.value) scanPhase.value = 'idle'
  }

  const retryScan = async () => {
    if (!lastScanFile.value || isScanning.value) return
    await scanReceipt(lastScanFile.value, lastScanGroupId.value)
  }

  const dismissScan = () => {
    scanError.value = null
    scanPhase.value = 'idle'
  }

  return {
    scanReceipt,
    cancelScan,
    retryScan,
    dismissScan,
    isScanning: readonly(isScanning),
    phase: readonly(scanPhase),
    scanError: readonly(scanError),
    receiptImageUrl: readonly(receiptImageUrl),
    clearReceiptImage,
  }
}
