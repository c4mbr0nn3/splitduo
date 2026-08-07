import { describe, it, expect, vi, beforeEach } from 'vitest'

import useErrorHandling from './useErrorHandling'
import { navigateTo } from '#app/composables/router'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const showErrorMock = vi.hoisted(() => vi.fn())

// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: tMock }),
}))

// useNotifications is auto-imported inside useErrorHandling.ts; mock the
// composable module so every toast call is recorded on showErrorMock.
vi.mock('~/composables/utils/useNotifications', () => ({
  default: () => ({ showError: showErrorMock }),
}))

describe('useErrorHandling', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('handleApiError', () => {
    it('extracts the message from the nested error.data.error.message shape', () => {
      const error = { data: { error: { message: 'Nested failure' } }, message: 'Top-level failure' }
      const { handleApiError } = useErrorHandling()

      handleApiError(error)

      expect(showErrorMock).toHaveBeenCalledWith('Nested failure')
    })

    it('falls back to error.message when data.error.message is absent', () => {
      const error = { data: { error: {} }, message: 'Top-level failure' }
      const { handleApiError } = useErrorHandling()

      handleApiError(error)

      expect(showErrorMock).toHaveBeenCalledWith('Top-level failure')
    })

    it('falls back to defaultMessage when no message can be extracted', () => {
      const { handleApiError } = useErrorHandling()

      handleApiError({ data: { error: {} } }, 'Fallback message')

      expect(showErrorMock).toHaveBeenCalledWith('Fallback message')
    })

    it('falls back to the default toast key when nothing else is available', () => {
      const { handleApiError } = useErrorHandling()

      handleApiError({})

      expect(tMock).toHaveBeenCalledWith('toasts.errors.default')
      expect(showErrorMock).toHaveBeenCalledWith('toasts.errors.default')
    })

    it('shows the default toast for null and undefined errors', () => {
      const { handleApiError } = useErrorHandling()

      handleApiError(null)
      handleApiError(undefined)

      expect(showErrorMock).toHaveBeenCalledTimes(2)
      expect(showErrorMock).toHaveBeenCalledWith('toasts.errors.default')
    })

    it('shows the default toast for non-object errors', () => {
      const { handleApiError } = useErrorHandling()

      handleApiError('boom')
      handleApiError(42)

      expect(showErrorMock).toHaveBeenCalledTimes(2)
      expect(showErrorMock).toHaveBeenCalledWith('toasts.errors.default')
    })
  })

  describe('handleValidationErrors', () => {
    it('shows one toast per error, passing field and message to t', () => {
      const { handleValidationErrors } = useErrorHandling()

      handleValidationErrors([
        { field: 'name', message: 'is required' },
        { field: 'email', message: 'is invalid' },
      ])

      expect(showErrorMock).toHaveBeenCalledTimes(2)
      expect(tMock).toHaveBeenCalledWith('toasts.errors.validationError', { field: 'name', message: 'is required' })
      expect(tMock).toHaveBeenCalledWith('toasts.errors.validationError', { field: 'email', message: 'is invalid' })
      expect(showErrorMock).toHaveBeenCalledWith('toasts.errors.validationError')
    })

    it('does nothing for non-array input', () => {
      const { handleValidationErrors } = useErrorHandling()

      handleValidationErrors({ field: 'name', message: 'is required' })
      handleValidationErrors('oops')

      expect(showErrorMock).not.toHaveBeenCalled()
    })

    it('does nothing for an empty array', () => {
      const { handleValidationErrors } = useErrorHandling()

      handleValidationErrors([])

      expect(showErrorMock).not.toHaveBeenCalled()
    })
  })

  describe('handleAuthError', () => {
    it('shows the auth-required toast and navigates home on 401', () => {
      const { handleAuthError } = useErrorHandling()

      handleAuthError({ statusCode: 401 })

      expect(showErrorMock).toHaveBeenCalledWith('toasts.errors.authRequired')
      expect(navigateTo).toHaveBeenCalledWith('/')
    })

    it('shows the access-denied toast without navigating on 403', () => {
      const { handleAuthError } = useErrorHandling()

      handleAuthError({ statusCode: 403 })

      expect(showErrorMock).toHaveBeenCalledWith('toasts.errors.accessDenied')
      expect(navigateTo).not.toHaveBeenCalled()
    })

    it('falls through to handleApiError for other status codes', () => {
      const { handleAuthError } = useErrorHandling()

      handleAuthError({ statusCode: 500, message: 'Server exploded' })

      expect(showErrorMock).toHaveBeenCalledWith('Server exploded')
      expect(navigateTo).not.toHaveBeenCalled()
    })

    it('falls through to handleApiError for non-object errors', () => {
      const { handleAuthError } = useErrorHandling()

      handleAuthError('unauthorized')

      expect(showErrorMock).toHaveBeenCalledWith('toasts.errors.default')
      expect(navigateTo).not.toHaveBeenCalled()
    })
  })
})
