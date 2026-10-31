// Custom SplitDuo service worker (injectManifest mode — vite-pwa does NOT
// generate any SW code for us; everything here is hand-implemented).
//
// Responsibilities:
// - Precache build assets + navigation fallback to /index.html (SPA)
// - NetworkOnly for /api/* (never cache API responses)
// - SKIP_WAITING message → skipWaiting() (prompt-update flow, PwaUpdate.vue)
// - Web Push: display notifications, handle click navigation, best-effort
//   resubscribe on pushsubscriptionchange
import { clientsClaim } from 'workbox-core'
import { cleanupOutdatedCaches, precacheAndRoute } from 'workbox-precaching'
import { NavigationRoute, registerRoute } from 'workbox-routing'
import { NetworkFirst, NetworkOnly } from 'workbox-strategies'

declare let self: ServiceWorkerGlobalScope

// Precache all build assets emitted by Vite (injected as __WB_MANIFEST).
// Hashed assets only — the SPA shell is handled by the 'app-shell' runtime
// cache below, NOT precached (a null-revision precache entry would pin a
// stale index.html and break after asset-changing deploys).
precacheAndRoute(self.__WB_MANIFEST)
cleanupOutdatedCaches()

// SPA navigation fallback: NetworkFirst — online serves a fresh index.html
// each visit (so newly hashed assets referenced by the shell always exist);
// offline falls back to the last cached shell.
registerRoute(new NavigationRoute(new NetworkFirst({ cacheName: 'app-shell' }), {
  denylist: [/^\/api\//],
}))

// Never cache API responses. Function matcher (RegExp routes match against
// the full url.href, which would never fire for this pattern).
registerRoute(({ url }) => url.origin === self.location.origin && url.pathname.startsWith('/api/'), new NetworkOnly())

// Prompt-update flow: PwaUpdate.vue calls updateServiceWorker(); the new SW
// waits for SKIP_WAITING before activating (registerType: 'prompt').
self.addEventListener('message', (event) => {
  if (event.data === 'SKIP_WAITING') {
    void self.skipWaiting()
  }
})

clientsClaim()

// Warm the app-shell cache so the first offline visit isn't a miss.
// Best-effort: failing to fetch (e.g. activated while offline) must not
// break activation.
try {
  void self.caches.open('app-shell').then(cache => cache.add(new URL('/index.html', self.location.origin)))
}
catch {
  // Ignore — NetworkFirst populates the cache on the first successful
  // navigation anyway.
}

// --- Web Push ---------------------------------------------------------------

interface PushPayload {
  title?: string
  body?: string
  tag?: string
  url?: string
}

self.addEventListener('push', (event: PushEvent) => {
  let payload: PushPayload = {}
  try {
    payload = (event.data?.json() as PushPayload | undefined) ?? {}
  }
  catch {
    // Unparseable payload — fall through to the generic notification
  }

  event.waitUntil(self.registration.showNotification(payload.title ?? 'SplitDuo', {
    body: payload.body,
    tag: payload.tag,
    icon: '/pwa-192x192.png',
    data: { url: payload.url ?? '/' },
  }))
})

// notificationclick: focus an existing window and tell it to navigate
// client-side, or open a new one. Never client.navigate() (deprecated).
self.addEventListener('notificationclick', (event: NotificationEvent) => {
  event.notification.close()

  const url = (event.notification.data as { url?: string } | undefined)?.url ?? '/'

  event.waitUntil((async () => {
    const clientList = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    for (const client of clientList) {
      if ('focus' in client) {
        await client.focus()
        client.postMessage({ type: 'NAVIGATE', url })
        return
      }
    }
    await self.clients.openWindow(new URL(url, self.location.origin).href)
  })())
})

// pushsubscriptionchange: browser rotated its keys or dropped the subscription.
// Re-subscribe locally only — the POST was removed because the service worker
// has no bearer token, so the request always 401'd. In Chrome the event often
// fires with null old/new subscriptions. The app's resubscribeIfNeeded() on
// mount is the real healing path for the server row.
self.addEventListener('pushsubscriptionchange', (event: PushSubscriptionChangeEvent) => {
  event.waitUntil((async () => {
    try {
      if (Notification.permission !== 'granted') return
      const existing = await self.registration.pushManager.getSubscription()
      if (existing) return
      // New subscription can be null if the browser refuses (no key available).
      const applicationServerKey = event.newSubscription?.options.applicationServerKey
      if (!applicationServerKey) return
      await self.registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey,
      })
      // The server row is healed by the app's resubscribeIfNeeded() on next
      // mount — the SW has no bearer token to update it directly.
    }
    catch (error) {
      // Give up silently — the app's resubscribeIfNeeded() heals on next mount.
      console.warn('[sw] pushsubscriptionchange handling failed:', error)
    }
  })())
})
