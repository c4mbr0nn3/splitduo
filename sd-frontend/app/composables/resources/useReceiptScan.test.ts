import { describe, it, expect, vi, beforeAll, beforeEach, afterEach } from 'vitest'

import { apiMock } from '~/composables/api/base.mock'
import type { ParsedReceipt } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))
const routerMock = vi.hoisted(() => ({
  back: vi.fn(),
  push: vi.fn(),
  replace: vi.fn(),
  currentRoute: { value: { path: '/' } },
}))

// useApi / useNotifications are auto-imported inside useReceiptScan.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n'; mock the module so `t` is a
// controllable passthrough that returns the message key. useRouter is mocked in
// vitest.setup.ts with fresh spies per call, so override it here with a stable
// hoisted mock to assert on `push`.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))
vi.mock('#app/composables/router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('#app/composables/router')>()
  return {
    ...actual,
    useRouter: () => routerMock,
  }
})

const parsedReceipt: ParsedReceipt = {
  title: 'Grocery run',
  amount: 42.5,
  description: 'Weekly groceries',
  expenseDate: '2026-08-01',
  categoryId: 1,
  paymentModeId: 2,
}

// A minimal File stub: happy-dom's File/Blob support is limited, and the
// compression pipeline (Image onload + canvas.toBlob) is not exercised here —
// the tests focus on the observable API + navigation behavior.
const receiptFile = (): File => new File(['fake-image-bytes'], 'receipt.jpg', { type: 'image/jpeg' })

// happy-dom's Image never fires onload for blob URLs, so compressImage would
// never resolve. Stub Image so onload fires synchronously with a known size;
// happy-dom's canvas.toBlob resolves with a real Blob asynchronously.
class FakeImage {
  width = 4000
  height = 3000
  onload: (() => void) | null = null
  private _src = ''
  get src(): string {
    return this._src
  }

  set src(value: string) {
    this._src = value
    this.onload?.()
  }
}

describe('useReceiptScan', () => {
  // The first dynamic import of the module graph pays a one-off transform cost
  // that can exceed the default 5000ms test timeout, so warm it up in beforeAll.
  beforeAll(async () => {
    await import('./useReceiptScan')
  })

  beforeEach(() => {
    vi.clearAllMocks()
    vi.stubGlobal('Image', FakeImage)
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  describe('scanReceipt', () => {
    it('compresses the image and posts the resulting blob to the parse endpoint', async () => {
      const createObjectURLSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake-image')
      const revokeObjectURLSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
      apiMock.post.mockResolvedValue({ success: true, data: parsedReceipt })
      const { default: useReceiptScan } = await import('./useReceiptScan')
      const scan = useReceiptScan()

      await scan.scanReceipt(receiptFile(), 'group-1')

      expect(createObjectURLSpy).toHaveBeenCalledWith(expect.any(File))
      expect(revokeObjectURLSpy).toHaveBeenCalledWith('blob:fake-image')
      expect(apiMock.post).toHaveBeenCalledWith('/receipts/parse', expect.any(FormData))
      const formData = apiMock.post.mock.calls[0]?.[1] as FormData
      expect(formData.get('image')).toBeInstanceOf(Blob)
      expect(routerMock.push).toHaveBeenCalledWith({
        path: '/expenses/add',
        query: {
          groupId: 'group-1',
          title: 'Grocery run',
          amount: 42.5,
          description: 'Weekly groceries',
          expenseDate: '2026-08-01',
          categoryId: 1,
          paymentModeId: 2,
        },
      })
      createObjectURLSpy.mockRestore()
      revokeObjectURLSpy.mockRestore()
    })

    it('posts the compressed image and navigates to the expense form with the parsed fields', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: parsedReceipt })
      const { default: useReceiptScan } = await import('./useReceiptScan')
      const scan = useReceiptScan()

      await scan.scanReceipt(receiptFile(), 'group-1')

      expect(apiMock.post).toHaveBeenCalledWith('/receipts/parse', expect.any(FormData))
      const formData = apiMock.post.mock.calls[0]?.[1] as FormData
      expect(formData.get('image')).toBeInstanceOf(Blob)
      expect(routerMock.push).toHaveBeenCalledWith({
        path: '/expenses/add',
        query: {
          groupId: 'group-1',
          title: 'Grocery run',
          amount: 42.5,
          description: 'Weekly groceries',
          expenseDate: '2026-08-01',
          categoryId: 1,
          paymentModeId: 2,
        },
      })
      expect(scan.isScanning.value).toBe(false)
    })

    it('omits groupId from the query when no group is provided', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: parsedReceipt })
      const { default: useReceiptScan } = await import('./useReceiptScan')
      const scan = useReceiptScan()

      await scan.scanReceipt(receiptFile())

      expect(routerMock.push).toHaveBeenCalledWith({
        path: '/expenses/add',
        query: {
          title: 'Grocery run',
          amount: 42.5,
          description: 'Weekly groceries',
          expenseDate: '2026-08-01',
          categoryId: 1,
          paymentModeId: 2,
        },
      })
    })

    it('omits optional fields that are absent from the parsed receipt', async () => {
      apiMock.post.mockResolvedValue({
        success: true,
        data: { title: 'Coffee', amount: 4, expenseDate: '2026-08-01' },
      })
      const { default: useReceiptScan } = await import('./useReceiptScan')
      const scan = useReceiptScan()

      await scan.scanReceipt(receiptFile(), 'group-1')

      expect(routerMock.push).toHaveBeenCalledWith({
        path: '/expenses/add',
        query: { groupId: 'group-1', title: 'Coffee', amount: 4, expenseDate: '2026-08-01' },
      })
    })

    it('shows an error toast and does not navigate when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const { default: useReceiptScan } = await import('./useReceiptScan')
      const scan = useReceiptScan()

      await scan.scanReceipt(receiptFile(), 'group-1')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.receipts.scanFailed')
      expect(routerMock.push).not.toHaveBeenCalled()
      expect(scan.isScanning.value).toBe(false)
    })

    it('shows an error toast and does not navigate when the response is unsuccessful', async () => {
      apiMock.post.mockResolvedValue({
        success: false,
        data: null,
        error: { code: 'PARSE_FAILED', message: 'Could not read the receipt' },
      })
      const { default: useReceiptScan } = await import('./useReceiptScan')
      const scan = useReceiptScan()

      await scan.scanReceipt(receiptFile(), 'group-1')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.receipts.scanFailed')
      expect(routerMock.push).not.toHaveBeenCalled()
    })

    it('sets isScanning during the call and clears it afterwards', async () => {
      let resolveScan: (value: unknown) => void = () => {}
      apiMock.post.mockImplementation(() => new Promise((resolve) => {
        resolveScan = resolve
      }))
      const { default: useReceiptScan } = await import('./useReceiptScan')
      const scan = useReceiptScan()

      // Let the synchronous Image onload + async canvas.toBlob settle so the
      // POST is issued before we assert on the loading flag.
      await new Promise(resolve => setTimeout(resolve, 0))
      const pending = scan.scanReceipt(receiptFile(), 'group-1')
      expect(scan.isScanning.value).toBe(true)

      // Wait for the async toBlob continuation so the POST is actually issued
      // before resolving it.
      await new Promise(resolve => setTimeout(resolve, 0))
      resolveScan({ success: true, data: parsedReceipt })
      await pending

      expect(scan.isScanning.value).toBe(false)
    })
  })
})
