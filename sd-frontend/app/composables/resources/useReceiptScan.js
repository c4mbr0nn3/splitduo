const receiptImageUrl = ref(null)

export default function useReceiptScan() {
  const api = useApi()
  const router = useRouter()
  const { showError } = useNotifications()
  const isScanning = ref(false)

  const clearReceiptImage = () => {
    if (receiptImageUrl.value) URL.revokeObjectURL(receiptImageUrl.value)
    receiptImageUrl.value = null
  }

  const compressImage = (file) => {
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
        canvas.getContext('2d').drawImage(img, 0, 0, width, height)
        URL.revokeObjectURL(img.src)
        canvas.toBlob(resolve, 'image/jpeg', 0.8)
      }
      img.src = URL.createObjectURL(file)
    })
  }

  const scanReceipt = async (file, groupId = null) => {
    isScanning.value = true
    try {
      const compressed = await compressImage(file)
      if (receiptImageUrl.value) URL.revokeObjectURL(receiptImageUrl.value)
      receiptImageUrl.value = URL.createObjectURL(compressed)
      const form = new FormData()
      form.append('image', compressed, 'receipt.jpg')

      const response = await api.post('/receipts/parse', form)
      if (!response.success) throw new Error(response.error || 'Scan failed')

      const { title, amount, description, expenseDate, categoryId, paymentModeId } = response.data
      const query = {}
      if (groupId) query.groupId = groupId
      if (title) query.title = title
      if (amount != null) query.amount = amount
      if (description) query.description = description
      if (expenseDate) query.expenseDate = expenseDate
      if (categoryId != null) query.categoryId = categoryId
      if (paymentModeId != null) query.paymentModeId = paymentModeId

      router.push({ path: '/expenses/add', query })
    }
    catch {
      showError('Failed to scan receipt. Please try again.')
    }
    finally {
      isScanning.value = false
    }
  }

  return {
    scanReceipt,
    isScanning: readonly(isScanning),
    receiptImageUrl: readonly(receiptImageUrl),
    clearReceiptImage,
  }
}
