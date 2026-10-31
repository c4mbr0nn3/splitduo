import { describe, it, expect, vi } from 'vitest'
import { nextTick, reactive } from 'vue'

import usePushSession from './usePushSession'

describe('usePushSession', () => {
  const setup = () => {
    const push = { init: vi.fn(), resubscribeIfNeeded: vi.fn() }
    // reactive: watch() only fires on reactive/ref sources, and useAuth's
    // isAuthenticated is a ref — a plain object would never trigger.
    const isAuthenticated = reactive({ value: false })
    const session = usePushSession(push, isAuthenticated)
    return { push, isAuthenticated, ...session }
  }

  it('does not bootstrap when anonymous and ensure() is called', () => {
    const { push, ensure } = setup()

    ensure()

    expect(push.init).not.toHaveBeenCalled()
    expect(push.resubscribeIfNeeded).not.toHaveBeenCalled()
  })

  it('bootstraps once when isAuthenticated flips to true (watcher → in-session login)', async () => {
    const { push, isAuthenticated, ensure } = setup()

    // Anonymous at mount, like a user landing on the login page.
    ensure()
    isAuthenticated.value = true
    await nextTick()

    expect(push.init).toHaveBeenCalledTimes(1)
    expect(push.resubscribeIfNeeded).toHaveBeenCalledTimes(1)
  })

  it('bootstraps exactly once across repeated ensure() calls after start', () => {
    const auth = reactive({ value: true })
    const push = { init: vi.fn(), resubscribeIfNeeded: vi.fn() }
    const { ensure } = usePushSession(push, auth)

    ensure()
    ensure()
    ensure()

    expect(push.init).toHaveBeenCalledTimes(1)
    expect(push.resubscribeIfNeeded).toHaveBeenCalledTimes(1)
  })

  it('bootstraps immediately on ensure() when authenticated at construction', () => {
    const auth = reactive({ value: true })
    const push = { init: vi.fn(), resubscribeIfNeeded: vi.fn() }
    const { ensure } = usePushSession(push, auth)

    ensure()

    expect(push.init).toHaveBeenCalledTimes(1)
    expect(push.resubscribeIfNeeded).toHaveBeenCalledTimes(1)
  })

  it('re-bootstraps after logout → login (gate resets on the false transition)', async () => {
    const { push, isAuthenticated } = setup()
    isAuthenticated.value = true
    await nextTick()
    expect(push.init).toHaveBeenCalledTimes(1)

    isAuthenticated.value = false
    await nextTick()
    isAuthenticated.value = true
    await nextTick()

    expect(push.init).toHaveBeenCalledTimes(2)
    expect(push.resubscribeIfNeeded).toHaveBeenCalledTimes(2)
  })
})
