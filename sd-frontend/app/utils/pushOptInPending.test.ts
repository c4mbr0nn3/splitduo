import { describe, expect, it, beforeEach } from 'vitest'
import { consumePushOptInPending, markPushOptInPending } from './pushOptInPending'

describe('pushOptInPending', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('returns the stored group name once, then null (one-shot)', () => {
    markPushOptInPending('Flat 7')

    expect(consumePushOptInPending()).toBe('Flat 7')
    expect(consumePushOptInPending()).toBeNull()
  })

  it('returns null when nothing was stored', () => {
    expect(consumePushOptInPending()).toBeNull()
  })

  it('round-trips an empty group name as "" — distinct from the absent marker', () => {
    markPushOptInPending('')

    expect(consumePushOptInPending()).toBe('')
    expect(consumePushOptInPending()).toBeNull()
  })
})
