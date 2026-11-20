import { describe, expect, it } from 'vitest'

import { isSkipWaitingMessage } from './skip-waiting'

describe('isSkipWaitingMessage', () => {
  it('accepts the object posted by workbox-window messageSkipWaiting', () => {
    expect(isSkipWaitingMessage({ type: 'SKIP_WAITING' })).toBe(true)
  })

  it('accepts the legacy bare string used by manual recovery', () => {
    expect(isSkipWaitingMessage('SKIP_WAITING')).toBe(true)
  })

  it.each([
    ['undefined', undefined],
    ['null', null],
    ['a different string', 'SKIP_WAITING_OTHER'],
    ['an object with another type', { type: 'OTHER' }],
    ['a number', 0],
    ['an array', ['SKIP_WAITING']],
  ])('rejects %s', (_label, value) => {
    expect(isSkipWaitingMessage(value)).toBe(false)
  })
})
