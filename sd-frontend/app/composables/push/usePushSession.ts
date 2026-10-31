import { watch } from 'vue'

interface PushBootstrap {
  init: () => void
  resubscribeIfNeeded: () => Promise<void>
}

/**
 * Bootstraps Web Push once per authenticated period: app mount covers a
 * restored session, and the auth watcher covers a login completed without a
 * reload. Logging out resets the gate, so a later login bootstraps again.
 */
export default function usePushSession(
  push: PushBootstrap,
  isAuthenticated: { readonly value: boolean },
) {
  let started = false

  const ensure = (): void => {
    if (started || !isAuthenticated.value) return
    started = true
    push.init()
    void push.resubscribeIfNeeded()
  }

  const reset = (): void => {
    started = false
  }

  // Getter source, not the raw object: watching a plain { value } object
  // deep-watches and hands the callback the object itself, so an explicit
  // true/false never reaches the handler and logout would not reset the gate.
  watch(() => isAuthenticated.value, (authenticated) => {
    if (authenticated) ensure()
    else reset()
  })

  return { ensure }
}
