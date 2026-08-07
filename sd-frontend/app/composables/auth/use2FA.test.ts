import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import use2FA from './use2FA'
import { apiMock } from '~/composables/api/base.mock'
import type { TwoFactorSetup } from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

// useApi / useNotifications are auto-imported inside use2FA.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n'; mock the module so `t` is a
// controllable passthrough that returns the message key.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))

const twoFactorSetup: TwoFactorSetup = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUri: 'otpauth://totp/SplitDuo:alice@example.com?secret=JBSWY3DPEHPK3PXP',
  backupCodes: ['1111-2222', '3333-4444'],
}

describe('use2FA', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('initiateSetup', () => {
    it('returns the TOTP setup data on success', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: twoFactorSetup })
      const twoFA = use2FA()

      const result = await twoFA.initiateSetup()

      expect(apiMock.post).toHaveBeenCalledWith('/2fa/setup/initiate')
      expect(result).toEqual(twoFactorSetup)
    })

    it('returns null when the response is unsuccessful', async () => {
      apiMock.post.mockResolvedValue({ success: false, data: null })
      const twoFA = use2FA()

      const result = await twoFA.initiateSetup()

      expect(result).toBeNull()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const twoFA = use2FA()

      await expect(twoFA.initiateSetup()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.2fa.initiateFailed')
    })

    it('sets isLoading during the call and clears it afterwards', async () => {
      let resolveSetup: (value: unknown) => void = () => {}
      apiMock.post.mockImplementation(() => new Promise((resolve) => {
        resolveSetup = resolve
      }))
      const twoFA = use2FA()

      const pending = twoFA.initiateSetup()
      expect(twoFA.isLoading.value).toBe(true)

      resolveSetup({ success: true, data: twoFactorSetup })
      await pending

      expect(twoFA.isLoading.value).toBe(false)
    })
  })

  describe('verifySetup', () => {
    it('returns true and shows a success toast when the code is accepted', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: null })
      const twoFA = use2FA()

      const result = await twoFA.verifySetup('123456')

      expect(apiMock.post).toHaveBeenCalledWith('/2fa/setup/verify', { code: '123456' })
      expect(result).toBe(true)
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.2fa.enabled')
    })

    it('returns false without a toast when the code is rejected', async () => {
      apiMock.post.mockResolvedValue({ success: false, data: null })
      const twoFA = use2FA()

      const result = await twoFA.verifySetup('000000')

      expect(result).toBe(false)
      expect(notificationsMock.showSuccess).not.toHaveBeenCalled()
      expect(notificationsMock.showError).not.toHaveBeenCalled()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const twoFA = use2FA()

      await expect(twoFA.verifySetup('123456')).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.2fa.invalidCode')
    })
  })

  describe('disable', () => {
    it('returns true and shows a success toast when 2FA is disabled', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: null })
      const twoFA = use2FA()

      const result = await twoFA.disable('password123')

      expect(apiMock.post).toHaveBeenCalledWith('/2fa/disable', { password: 'password123' })
      expect(result).toBe(true)
      expect(notificationsMock.showSuccess).toHaveBeenCalledWith('toasts.2fa.disabled')
    })

    it('returns false when the response is unsuccessful', async () => {
      apiMock.post.mockResolvedValue({ success: false, data: null })
      const twoFA = use2FA()

      const result = await twoFA.disable('wrong-password')

      expect(result).toBe(false)
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const twoFA = use2FA()

      await expect(twoFA.disable('password123')).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.2fa.disableFailed')
    })
  })

  describe('generateBackupCodes', () => {
    it('returns the generated backup codes on success', async () => {
      apiMock.post.mockResolvedValue({ success: true, data: ['aaaa-bbbb', 'cccc-dddd'] })
      const twoFA = use2FA()

      const result = await twoFA.generateBackupCodes()

      expect(apiMock.post).toHaveBeenCalledWith('/2fa/backup-codes/generate')
      expect(result).toEqual(['aaaa-bbbb', 'cccc-dddd'])
    })

    it('returns null when the response is unsuccessful', async () => {
      apiMock.post.mockResolvedValue({ success: false, data: null })
      const twoFA = use2FA()

      const result = await twoFA.generateBackupCodes()

      expect(result).toBeNull()
    })

    it('shows an error toast and re-throws when the API call fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Network down'))
      const twoFA = use2FA()

      await expect(twoFA.generateBackupCodes()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.2fa.backupCodesFailed')
    })
  })
})
