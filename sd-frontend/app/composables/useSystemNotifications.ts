import type { AdminNotification, DismissNotificationRequest } from '~/types/domain'

// Module-level singleton state — shared across all consumers (see useAiStatus).
const globalNotifications = ref<AdminNotification[]>([])
const globalIsInitialized = ref(false)
let fetchPromise: Promise<void> | null = null

export default function useSystemNotifications() {
  const api = useApi()
  const { isGlobalAdmin } = useAuth()

  const fetchSystemNotifications = async (): Promise<void> => {
    // Admin-gated: non-admins never hit the endpoint (403) — skip entirely.
    if (!isGlobalAdmin.value) return
    if (globalIsInitialized.value) return
    if (fetchPromise) return fetchPromise

    fetchPromise = (async () => {
      try {
        const response = await api.get<AdminNotification[]>('/admin/notifications')
        if (response.success && response.data) globalNotifications.value = response.data
        globalIsInitialized.value = true
      }
      catch {
        // Silent — notifications just stay hidden
      }
      finally {
        fetchPromise = null
      }
    })()

    return fetchPromise
  }

  const refetch = async (): Promise<void> => {
    globalIsInitialized.value = false
    await fetchSystemNotifications()
  }

  const dismiss = async (type: string, targetKey: string): Promise<void> => {
    const body: DismissNotificationRequest = { type, targetKey }
    try {
      await api.post('/admin/notifications/dismiss', body)
      // Server-side dismissal succeeded — remove locally, no refetch needed.
      globalNotifications.value = globalNotifications.value.filter(
        n => !(n.type === type && n.targetKey === targetKey),
      )
    }
    catch {
      // Silent — leave the notification visible if the dismissal failed
    }
  }

  return {
    notifications: readonly(globalNotifications),
    fetchSystemNotifications,
    refetch,
    dismiss,
  }
}
