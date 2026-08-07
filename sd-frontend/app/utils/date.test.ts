import { describe, it, expect } from 'vitest'
import { formatDate, formatDateString, formatDuration } from './date'

// Deterministic reference timestamp: UTC midnight avoids date-boundary shifts
// in the test environment's timezone (locale is stubbed to 'en' in vitest.setup.ts).
const JAN_15_2024_SECONDS = new Date('2024-01-15T00:00:00Z').getTime() / 1000

describe('formatDate', () => {
  it('formats a valid Unix timestamp as a localized date', () => {
    expect(formatDate(JAN_15_2024_SECONDS)).toBe('Jan 15, 2024')
  })

  it('accepts a numeric string timestamp', () => {
    expect(formatDate(String(JAN_15_2024_SECONDS))).toBe('Jan 15, 2024')
  })

  it('returns Unknown for a zero timestamp', () => {
    expect(formatDate(0)).toBe('Unknown')
  })

  it('returns Unknown for an empty string', () => {
    expect(formatDate('')).toBe('Unknown')
  })

  it('returns Unknown for null', () => {
    // @ts-expect-error -- runtime guard for falsy input not expressible in the signature
    expect(formatDate(null)).toBe('Unknown')
  })
})

describe('formatDateString', () => {
  it('formats an ISO date string', () => {
    expect(formatDateString('2024-01-15T00:00:00Z')).toBe('Jan 15, 2024')
  })

  it('formats a Date object', () => {
    expect(formatDateString(new Date('2024-01-15T00:00:00Z'))).toBe('Jan 15, 2024')
  })

  it('returns Unknown for an empty string', () => {
    expect(formatDateString('')).toBe('Unknown')
  })

  it('returns Unknown for null', () => {
    // @ts-expect-error -- runtime guard for falsy input not expressible in the signature
    expect(formatDateString(null)).toBe('Unknown')
  })
})

describe('formatDuration', () => {
  it('formats sub-second durations in milliseconds', () => {
    expect(formatDuration(347)).toBe('347ms')
    expect(formatDuration(0)).toBe('0ms')
    expect(formatDuration(999)).toBe('999ms')
  })

  it('formats sub-10-second durations with 2 decimals', () => {
    expect(formatDuration(1200)).toBe('1.2s')
    expect(formatDuration(1234)).toBe('1.23s')
    expect(formatDuration(9500)).toBe('9.5s')
  })

  it('formats 10-60 second durations with 1 decimal', () => {
    expect(formatDuration(10000)).toBe('10s')
    expect(formatDuration(10500)).toBe('10.5s')
    expect(formatDuration(59500)).toBe('59.5s')
  })

  it('formats sub-hour durations as minutes and seconds', () => {
    expect(formatDuration(60000)).toBe('1m')
    expect(formatDuration(65000)).toBe('1m 5s')
    expect(formatDuration(3599000)).toBe('59m 59s')
  })

  it('formats hour durations with optional minutes and seconds', () => {
    expect(formatDuration(3600000)).toBe('1h')
    expect(formatDuration(3660000)).toBe('1h 1m')
    expect(formatDuration(7380000)).toBe('2h 3m')
    expect(formatDuration(7385000)).toBe('2h 3m 5s')
  })

  it('returns N/A for null and NaN', () => {
    expect(formatDuration(NaN)).toBe('N/A')
    // @ts-expect-error -- runtime guard for null not expressible in the signature
    expect(formatDuration(null)).toBe('N/A')
  })
})
