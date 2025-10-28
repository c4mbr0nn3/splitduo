import { useOverlay } from '#ui/composables/useOverlay'
import GenericModal from '~/components/ui/GenericModal.vue'

/**
 * Composable for programmatically controlling modals
 * Provides convenience methods for common dialog types
 */
export default function useModal() {
  // Create modal overlay factory
  const overlay = useOverlay()
  const modal = overlay.create(GenericModal)

  /**
   * Open a generic modal with full customization
   * @param {Object} options - Modal options
   * @param {string} options.title - Modal title
   * @param {string} [options.subtitle] - Modal subtitle
   * @param {string} [options.content] - Modal content (HTML supported)
   * @param {string} [options.color='primary'] - Modal color theme
   * @param {string} [options.icon] - Icon name (e.g., 'i-lucide-alert-circle')
   * @param {string} [options.iconColor] - Icon color (defaults to color)
   * @param {string} [options.confirmText='Confirm'] - Confirm button text
   * @param {string} [options.cancelText='Cancel'] - Cancel button text
   * @param {string} [options.confirmColor] - Confirm button color (defaults to color)
   * @param {string} [options.cancelColor='neutral'] - Cancel button color
   * @param {boolean} [options.loading=false] - Loading state
   * @returns {Promise<boolean>} Promise resolving to true (confirmed), false (cancelled), or null (dismissed)
   */
  const open = async (options = {}) => {
    try {
      const result = await modal.open({
        title: '',
        subtitle: '',
        content: '',
        color: 'primary',
        icon: '',
        iconColor: '',
        confirmText: 'Confirm',
        cancelText: 'Cancel',
        confirmColor: '',
        cancelColor: 'neutral',
        loading: false,
        ...options,
      })
      return result
    }
    catch {
      // Modal was dismissed (escape key, overlay click, etc.)
      return null
    }
  }

  /**
   * Open a confirmation dialog
   * @param {Object} options - Confirmation options
   * @returns {Promise<boolean>} Promise resolving to true (confirmed) or false (cancelled)
   */
  const confirm = async (options = {}) => {
    return await open({
      color: 'primary',
      confirmText: 'Confirm',
      cancelText: 'Cancel',
      ...options,
    })
  }

  /**
   * Open an info dialog (blue theme)
   * @param {Object} options - Info dialog options
   * @returns {Promise<boolean>} Promise resolving to true (confirmed) or false (cancelled)
   */
  const info = async (options = {}) => {
    return await open({
      color: 'secondary',
      icon: 'i-lucide-info',
      confirmText: 'OK',
      cancelText: 'Cancel',
      ...options,
    })
  }

  /**
   * Open a success dialog (green theme)
   * @param {Object} options - Success dialog options
   * @returns {Promise<boolean>} Promise resolving to true (confirmed) or false (cancelled)
   */
  const success = async (options = {}) => {
    return await open({
      color: 'success',
      icon: 'i-lucide-check-circle',
      confirmText: 'OK',
      cancelText: 'Cancel',
      ...options,
    })
  }

  /**
   * Open a warning dialog (yellow theme)
   * @param {Object} options - Warning dialog options
   * @returns {Promise<boolean>} Promise resolving to true (confirmed) or false (cancelled)
   */
  const warning = async (options = {}) => {
    return await open({
      color: 'warning',
      icon: 'i-lucide-alert-triangle',
      confirmText: 'OK',
      cancelText: 'Cancel',
      ...options,
    })
  }

  /**
   * Open an error dialog (red theme)
   * @param {Object} options - Error dialog options
   * @returns {Promise<boolean>} Promise resolving to true (confirmed) or false (cancelled)
   */
  const error = async (options = {}) => {
    return await open({
      color: 'error',
      icon: 'i-lucide-x-circle',
      confirmText: 'OK',
      cancelText: 'Cancel',
      ...options,
    })
  }

  return {
    open,
    confirm,
    info,
    success,
    warning,
    error,
  }
}
