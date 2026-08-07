import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { withMinDuration } from './withMinDuration'

describe('withMinDuration', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('waits until the minimum duration has elapsed for fast work', async () => {
    const promise = withMinDuration(() => Promise.resolve('done'), 50)

    // Advance past the minimum duration
    vi.advanceTimersByTime(50)
    const result = await promise

    expect(result).toBe('done')
  })

  it('resolves immediately when the work takes longer than the minimum', async () => {
    const work = vi.fn(() => new Promise(resolve => setTimeout(resolve, 100)))
    const promise = withMinDuration(work, 50)

    // Work takes 100ms — advance past it
    vi.advanceTimersByTime(100)
    const result = await promise

    expect(result).toBeUndefined()
    expect(work).toHaveBeenCalledOnce()
  })

  it('returns the value resolved by the wrapped work', async () => {
    const promise = withMinDuration(() => Promise.resolve(42), 10)

    vi.advanceTimersByTime(10)
    const result = await promise

    expect(result).toBe(42)
  })
})
