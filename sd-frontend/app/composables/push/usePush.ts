// Web Push subscription management. Backed by the backend push endpoints:
// GET /push/vapid-public-key, GET|POST|DELETE /push/subscriptions.
// Types are local on purpose, not because api.d.ts lacks them:
// PushSubscriptionJson mirrors the browser's PushSubscription.toJSON() (not an API
// DTO, absent from api.d.ts), and PushSubscriptionListItem is deliberately narrower
// than the generated PushSubscriptionListDto (it requires id/endpoint and numbers).

export interface PushSubscriptionJson {
  endpoint: string
  expirationTime: number | null
  keys: {
    p256dh: string
    auth: string
  }
}

export interface PushSubscriptionListItem {
  id: string
  endpoint: string
  userAgent: string | null
  // Unix seconds (backend `long` from AuditableEntity.CreatedAt)
  createdAt: number
  lastUsedAt: number | null
}

// Singleton state — shared across all consumers.
const isSupported = ref(false)
const permission = ref<NotificationPermission | 'default'>('default')
const isSubscribed = ref(false)
const isConfigured = ref<boolean | null>(null)

export default function usePush() {
  const api = useApi()
  const { t } = useI18n()
  const notifications = useNotifications()

  if (import.meta.client) {
    isSupported.value = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
    permission.value = 'Notification' in window ? Notification.permission : 'default'
  }

  const getVapidPublicKey = async (): Promise<string | null> => {
    try {
      const response = await api.get<{ publicKey: string }>('/push/vapid-public-key')
      if (response.success && response.data) {
        isConfigured.value = true
        return response.data.publicKey
      }
      isConfigured.value = false
      return null
    }
    catch (error: unknown) {
      // 503 PUSH_NOT_CONFIGURED → push disabled server-side; network errors
      // leave isConfigured unknown (null).
      if (typeof error === 'object' && error !== null && 'statusCode' in error) {
        const statusCode = (error as { statusCode?: unknown }).statusCode
        if (statusCode === 503) {
          isConfigured.value = false
          return null
        }
      }
      return null
    }
  }

  const checkConfiguration = async (): Promise<boolean | null> => {
    // getVapidPublicKey() updates isConfigured as a side effect
    await getVapidPublicKey()
    return isConfigured.value
  }

  const saveSubscription = async (subscription: PushSubscriptionJson): Promise<boolean> => {
    try {
      await api.post('/push/subscriptions', {
        ...subscription,
        userAgent: navigator.userAgent,
      })
      return true
    }
    catch {
      return false
    }
  }

  const getRegistrationSubscription = async (): Promise<PushSubscription | null> => {
    if (!isSupported.value) return null
    const registration = await navigator.serviceWorker.ready
    return registration.pushManager.getSubscription()
  }

  const subscribe = async (): Promise<boolean> => {
    if (!isSupported.value) return false
    // One awaited network call before requestPermission is acceptable — same
    // pattern as getVapidPublicKey() — but it must resolve before the prompt to
    // avoid asking the user to enable push the server can't fulfill.
    if (isConfigured.value === null) await checkConfiguration()
    if (isConfigured.value === false) {
      notifications.showError(t('push.settings.notConfigured'))
      return false
    }
    if (isSubscribed.value) return true

    // Must be called synchronously within the user-gesture call chain.
    const result = await Notification.requestPermission()
    permission.value = result
    if (result !== 'granted') {
      notifications.showError(t('push.permissionDenied'))
      return false
    }

    try {
      const vapidKey = await getVapidPublicKey()
      if (!vapidKey) {
        notifications.showError(t('push.subscribeFailed'))
        return false
      }

      const registration = await navigator.serviceWorker.ready
      const pushSubscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: vapidKey,
      })
      const json = pushSubscription.toJSON() as unknown as PushSubscriptionJson

      const saved = await saveSubscription(json)
      if (!saved) {
        await pushSubscription.unsubscribe()
        notifications.showError(t('push.subscribeFailed'))
        return false
      }

      isSubscribed.value = true
      notifications.showSuccess(t('push.subscribed'))
      return true
    }
    catch {
      notifications.showError(t('push.subscribeFailed'))
      return false
    }
  }

  const unsubscribe = async (): Promise<boolean> => {
    try {
      const subscription = await getRegistrationSubscription()
      if (subscription) {
        await subscription.unsubscribe()
        // Only hit the backend when a browser subscription actually existed —
        // no endpoint to remove otherwise.
        try {
          await api.delete('/push/subscriptions', { endpoint: subscription.endpoint })
        }
        catch {
          // Best-effort server removal — browser unsubscribe already done
        }
      }
      isSubscribed.value = false
      notifications.showSuccess(t('push.unsubscribed'))
      return true
    }
    catch {
      notifications.showError(t('push.unsubscribeFailed'))
      return false
    }
  }

  const fetchSubscriptions = async (): Promise<PushSubscriptionListItem[]> => {
    try {
      const response = await api.get<PushSubscriptionListItem[]>('/push/subscriptions')
      return response.success && response.data ? response.data : []
    }
    catch {
      return []
    }
  }

  // Remove one row from the account's subscription list (e.g. an old device).
  // Local subscription is untouched — only the server record for this endpoint.
  // NOTE: covered by usePush.test.ts colocated in this directory (not in the
  // repo root test glob) — run `pnpm test` from sd-frontend.
  const removeSubscription = async (endpoint: string): Promise<boolean> => {
    try {
      await api.delete('/push/subscriptions', { endpoint })
      notifications.showSuccess(t('push.subscriptionRemoved'))
      return true
    }
    catch {
      notifications.showError(t('push.subscriptionRemoveFailed'))
      return false
    }
  }

  const resubscribeIfNeeded = async (): Promise<void> => {
    if (!isSupported.value || permission.value !== 'granted') return
    try {
      const subscription = await getRegistrationSubscription()
      if (!subscription) return // never auto-subscribe
      const json = subscription.toJSON() as unknown as PushSubscriptionJson
      await saveSubscription(json) // idempotent upsert — silent healing
      isSubscribed.value = true
    }
    catch {
      // Silent — no toasts from background healing
    }
  }

  const init = (): void => {
    if (!import.meta.client) return
    // Fire-and-forget: update isConfigured without blocking app mount, and
    // reflect the real browser subscription state immediately (so the
    // settings toggle doesn't wait for resubscribeIfNeeded).
    if (isSupported.value) {
      void getRegistrationSubscription()
        .then((subscription) => {
          isSubscribed.value = !!subscription
        })
        .catch(() => {
          // Keep whatever state we had
        })
    }
    void getVapidPublicKey()
  }

  // Contextual opt-in prompt gate: only when push is possible, not yet
  // decided, and the user hasn't explicitly dismissed the prompt before.
  // True means "show the PushOptInModal now".
  const shouldShowPushOptIn = (): boolean => {
    if (!import.meta.client) return false
    if (!useAuth().isAuthenticated.value) return false
    if (localStorage.getItem('push-prompt-dismissed') === '1') return false
    return isSupported.value
      && isConfigured.value !== false
      && permission.value !== 'granted'
      && !isSubscribed.value
  }

  return {
    isSupported: readonly(isSupported),
    permission: readonly(permission),
    isConfigured: readonly(isConfigured),
    isSubscribed: readonly(isSubscribed),
    checkConfiguration,
    subscribe,
    unsubscribe,
    fetchSubscriptions,
    removeSubscription,
    resubscribeIfNeeded,
    shouldShowPushOptIn,
    init,
  }
}
