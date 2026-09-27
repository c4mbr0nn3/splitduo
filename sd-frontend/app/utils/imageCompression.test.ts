import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import { compressImageForUpload, encodeAsJpeg } from './imageCompression'

// happy-dom never fires onload for blob URLs, so stub Image so onload/onerror
// fire synchronously with a known size.
class FakeImage {
  width = 4000
  height = 3000
  onload: (() => void) | null = null
  onerror: (() => void) | null = null
  private _src = ''
  get src(): string {
    return this._src
  }

  set src(value: string) {
    this._src = value
    this.onload?.()
  }
}

class ErrorImage {
  width = 0
  height = 0
  onload: (() => void) | null = null
  onerror: (() => void) | null = null
  private _src = ''
  get src(): string {
    return this._src
  }

  set src(value: string) {
    this._src = value
    this.onerror?.()
  }
}

const jpegFile = (): File => new File(['fake-image-bytes'], 'photo.jpg', { type: 'image/jpeg' })

// happy-dom has no canvas adapter (getContext returns null, so drawImage is
// skipped) — stub toBlob to synchronously yield a JPEG blob.
const stubCanvasToBlob = (): void => {
  vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(
    (callback: BlobCallback) => callback(new Blob(['compressed'], { type: 'image/jpeg' })),
  )
}

describe('imageCompression', () => {
  beforeEach(() => {
    vi.stubGlobal('Image', FakeImage)
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake-image')
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    stubCanvasToBlob()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  describe('encodeAsJpeg', () => {
    it('returns a jpeg blob when decoding succeeds', async () => {
      const blob = await encodeAsJpeg(jpegFile())

      expect(blob).toBeInstanceOf(Blob)
      expect(blob?.type).toBe('image/jpeg')
    })

    it('returns null when the image fails to decode', async () => {
      vi.stubGlobal('Image', ErrorImage)

      const blob = await encodeAsJpeg(jpegFile())

      expect(blob).toBeNull()
    })
  })

  describe('compressImageForUpload', () => {
    it('compresses a jpeg and keeps a .jpg filename', async () => {
      const result = await compressImageForUpload(jpegFile())

      expect(result.name).toBe('photo.jpg')
      expect(result.type).toBe('image/jpeg')
    })

    it('renames a png to .jpg after re-encoding', async () => {
      const png = new File(['fake'], 'screenshot.png', { type: 'image/png' })

      const result = await compressImageForUpload(png)

      expect(result.name).toBe('screenshot.jpg')
      expect(result.type).toBe('image/jpeg')
    })

    it('passes through a pdf untouched', async () => {
      const pdf = new File(['%PDF-fake'], 'doc.pdf', { type: 'application/pdf' })

      const result = await compressImageForUpload(pdf)

      expect(result).toBe(pdf)
    })

    it('passes through a heic untouched', async () => {
      const heic = new File(['fake'], 'photo.heic', { type: 'image/heic' })

      const result = await compressImageForUpload(heic)

      expect(result).toBe(heic)
    })

    it('falls back to the original file when decoding fails', async () => {
      vi.stubGlobal('Image', ErrorImage)
      const file = jpegFile()

      const result = await compressImageForUpload(file)

      expect(result).toBe(file)
    })
  })
})
