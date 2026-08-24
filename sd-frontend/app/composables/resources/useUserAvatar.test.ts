import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import useUserAvatar from './useUserAvatar'
import { apiMock } from '~/composables/api/base.mock'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside useUserAvatar.ts;
// mock the composable modules so every API call and toast is controlled from
// the test. useI18n is auto-imported from 'vue-i18n'; mock the module so `t`
// is a controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const avatarFile = (): File => new File([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], 'avatar.jpg', { type: 'image/jpeg' })

describe('useUserAvatar', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('uploadAvatar', () => {
    it('puts the file as multipart form-data, shows a success toast, returns true, and clears isLoading', async () => {
      apiMock.put.mockResolvedValue({ success: true, data: null })
      const avatar = useUserAvatar()

      const result = await avatar.uploadAvatar(avatarFile())

      expect(apiMock.put).toHaveBeenCalledWith('/users/me/avatar', expect.any(FormData))
      const formData = apiMock.put.mock.calls[0]?.[1] as FormData
      expect(formData.get('file')).toBeInstanceOf(File)
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.avatar.uploaded')
      expect(result).toBe(true)
      expect(avatar.isLoading.value).toBe(false)
    })

    it('builds the form-data with the uploaded file', async () => {
      apiMock.put.mockResolvedValue({ success: true, data: null })
      const avatar = useUserAvatar()
      const file = avatarFile()

      await avatar.uploadAvatar(file)

      const formData = apiMock.put.mock.calls[0]?.[1] as FormData
      const sentFile = formData.get('file')
      expect(sentFile).toBeInstanceOf(File)
      expect(sentFile).toMatchObject({ name: 'avatar.jpg', type: 'image/jpeg' })
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.put.mockRejectedValue(new Error('Upload failed'))
      const avatar = useUserAvatar()

      await expect(avatar.uploadAvatar(avatarFile())).rejects.toThrow('Upload failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.avatar.uploadFailed')
      expect(avatar.isLoading.value).toBe(false)
    })
  })

  describe('deleteAvatar', () => {
    it('deletes the avatar, shows a success toast, and returns true', async () => {
      apiMock.delete.mockResolvedValue({ success: true, data: null })
      const avatar = useUserAvatar()

      const result = await avatar.deleteAvatar()

      expect(apiMock.delete).toHaveBeenCalledWith('/users/me/avatar')
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.avatar.deleted')
      expect(result).toBe(true)
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Delete failed'))
      const avatar = useUserAvatar()

      await expect(avatar.deleteAvatar()).rejects.toThrow('Delete failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.avatar.deleteFailed')
    })
  })

  describe('getAvatarUrl', () => {
    it('fetches the blob and returns an object URL', async () => {
      const blob = new Blob(['fake-image-bytes'], { type: 'image/jpeg' })
      apiMock.getBlob.mockResolvedValue({ blob, headers: new Headers() })
      const createObjectURLSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake-avatar')
      const avatar = useUserAvatar()

      const url = await avatar.getAvatarUrl('user-1')

      expect(apiMock.getBlob).toHaveBeenCalledWith('/users/user-1/avatar')
      expect(createObjectURLSpy).toHaveBeenCalledWith(blob)
      expect(url).toBe('blob:fake-avatar')
      createObjectURLSpy.mockRestore()
    })

    it('returns null without showing a toast when the API call fails', async () => {
      apiMock.getBlob.mockRejectedValue(new Error('Not found'))
      const avatar = useUserAvatar()

      const url = await avatar.getAvatarUrl('user-1')

      expect(url).toBeNull()
      expect(notificationsMock.showError).not.toHaveBeenCalled()
    })

    it('returns null without calling the API when userId is empty', async () => {
      const avatar = useUserAvatar()

      const url = await avatar.getAvatarUrl('')

      expect(apiMock.getBlob).not.toHaveBeenCalled()
      expect(url).toBeNull()
    })
  })
})
