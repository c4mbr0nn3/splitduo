// Message contract between the page and this service worker for the prompt-update
// flow. `updateServiceWorker()` (vite-plugin-pwa) delegates to workbox-window's
// `messageSkipWaiting()`, which posts `{ type: 'SKIP_WAITING' }` — an object, not a
// bare string. Older SplitDuo builds only matched the string, so the update prompt
// sent the object, the worker ignored it, and the new worker stayed in `waiting`
// forever. The bare string stays accepted so a manual postMessage can recover a
// client stuck on an old build.
const SKIP_WAITING = 'SKIP_WAITING'

export function isSkipWaitingMessage(data: unknown): boolean {
  if (data === SKIP_WAITING) {
    return true
  }
  return typeof data === 'object' && data !== null && (data as { type?: unknown }).type === SKIP_WAITING
}
