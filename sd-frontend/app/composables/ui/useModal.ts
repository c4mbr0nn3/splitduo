import { useOverlay } from '#ui/composables/useOverlay'
import GenericModal from '~/components/ui/GenericModal.vue'

export type ButtonColor = 'primary' | 'secondary' | 'success' | 'error' | 'info' | 'warning' | 'neutral'

export interface ModalOptions {
  title?: string
  subtitle?: string
  content?: string
  color?: ButtonColor
  icon?: string
  iconColor?: string
  confirmText?: string
  cancelText?: string
  confirmColor?: ButtonColor | ''
  cancelColor?: ButtonColor
  loading?: boolean
}

/**
 * Composable for programmatically controlling modals
 * Provides convenience methods for common dialog types
 */
export default function useModal() {
  const { t } = useI18n()
  // Create modal overlay factory
  const overlay = useOverlay()
  const modal = overlay.create(GenericModal)

  /**
   * Open a generic modal with full customization
   * @returns Promise resolving to true (confirmed), false (cancelled), or null (dismissed)
   */
  const open = async (options: Partial<ModalOptions> = {}): Promise<boolean | null> => {
    try {
      const result = await modal.open({
        title: '',
        subtitle: '',
        content: '',
        color: 'primary',
        icon: '',
        iconColor: '',
        confirmText: t('common.confirm'),
        cancelText: t('common.cancel'),
        confirmColor: '',
        cancelColor: 'neutral',
        loading: false,
        ...options,
      })
      return result as boolean | null
    }
    catch {
      // Modal was dismissed (escape key, overlay click, etc.)
      return null
    }
  }

  /**
   * Open a confirmation dialog
   * @returns Promise resolving to true (confirmed) or false (cancelled)
   */
  const confirm = async (options: Partial<ModalOptions> = {}): Promise<boolean | null> => {
    return await open({
      color: 'primary',
      confirmText: t('common.confirm'),
      cancelText: t('common.cancel'),
      ...options,
    })
  }

  /**
   * Open an info dialog (blue theme)
   * @returns Promise resolving to true (confirmed) or false (cancelled)
   */
  const info = async (options: Partial<ModalOptions> = {}): Promise<boolean | null> => {
    return await open({
      color: 'secondary',
      icon: 'i-lucide-info',
      confirmText: t('common.ok'),
      cancelText: t('common.cancel'),
      ...options,
    })
  }

  /**
   * Open a success dialog (green theme)
   * @returns Promise resolving to true (confirmed) or false (cancelled)
   */
  const success = async (options: Partial<ModalOptions> = {}): Promise<boolean | null> => {
    return await open({
      color: 'success',
      icon: 'i-lucide-check-circle',
      confirmText: t('common.ok'),
      cancelText: t('common.cancel'),
      ...options,
    })
  }

  /**
   * Open a warning dialog (yellow theme)
   * @returns Promise resolving to true (confirmed) or false (cancelled)
   */
  const warning = async (options: Partial<ModalOptions> = {}): Promise<boolean | null> => {
    return await open({
      color: 'warning',
      icon: 'i-lucide-alert-triangle',
      confirmText: t('common.ok'),
      cancelText: t('common.cancel'),
      ...options,
    })
  }

  /**
   * Open an error dialog (red theme)
   * @returns Promise resolving to true (confirmed) or false (cancelled)
   */
  const error = async (options: Partial<ModalOptions> = {}): Promise<boolean | null> => {
    return await open({
      color: 'error',
      icon: 'i-lucide-x-circle',
      confirmText: t('common.ok'),
      cancelText: t('common.cancel'),
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
