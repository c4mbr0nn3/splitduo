// Canvas-decodable raster formats. HEIC/HEIF/PDF and anything else is passed
// through untouched (browser canvas cannot reliably decode them).
const COMPRESSIBLE_MIME_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp'])

const MAX_DIMENSION = 2000
const JPEG_QUALITY = 0.8

/**
 * Re-encodes an image as JPEG (max `MAX_DIMENSION` px on the longest side) via
 * canvas. Resolves null when the image cannot be decoded. Callers MUST fall
 * back to the original file in that case.
 *
 * Behaviour is intentionally identical to the routine previously inlined in
 * useReceiptScan.ts: a missing 2d context does not abort — toBlob is still
 * called (this keeps the happy-dom test environment working).
 */
export const encodeAsJpeg = (file: File): Promise<Blob | null> => {
  return new Promise((resolve) => {
    const img = new Image()
    img.onload = () => {
      let { width, height } = img
      if (width > MAX_DIMENSION || height > MAX_DIMENSION) {
        const ratio = Math.min(MAX_DIMENSION / width, MAX_DIMENSION / height)
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
      }, 'image/jpeg', JPEG_QUALITY)
    }
    img.onerror = () => {
      URL.revokeObjectURL(img.src)
      resolve(null)
    }
    img.src = URL.createObjectURL(file)
  })
}

/**
 * Compresses a user-picked file for attachment upload when it is a
 * canvas-decodable image. Returns the ORIGINAL file unchanged for PDFs,
 * HEIC/HEIF, non-images, and whenever compression fails — an upload must never
 * be lost because of a client-side optimization. Re-encoded images are renamed
 * to `.jpg` (the backend validates extension + sniffs the JPEG magic number,
 * both of which accept this).
 */
export const compressImageForUpload = async (file: File): Promise<File> => {
  if (!COMPRESSIBLE_MIME_TYPES.has(file.type)) return file
  try {
    const blob = await encodeAsJpeg(file)
    if (!blob) return file
    const name = file.name.replace(/\.[^.]+$/, '') + '.jpg'
    return new File([blob], name, { type: 'image/jpeg' })
  }
  catch {
    return file
  }
}
