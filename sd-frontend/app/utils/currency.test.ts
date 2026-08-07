import { describe, it, expect } from 'vitest'
import { toMillis, fromMillis, splitMillis, rescaleMillis, formatAmount, formatCurrency } from './currency'

describe('currency', () => {
  describe('toMillis / fromMillis', () => {
    it('round-trips integer millicents through decimal cents', () => {
      const millis = 12345
      expect(fromMillis(toMillis(millis))).toBe(millis)
    })

    it('converts string inputs to millicents', () => {
      expect(toMillis('10.1')).toBe(10100)
    })

    it('maps 0, NaN and empty input to 0 millicents', () => {
      expect(toMillis(0)).toBe(0)
      expect(toMillis(NaN)).toBe(0)
      expect(toMillis('')).toBe(0)
    })

    it('rounds to the nearest millicent', () => {
      expect(toMillis(3.3349)).toBe(3335)
      expect(toMillis(3.3344)).toBe(3334)
    })
  })

  describe('splitMillis', () => {
    it('distributes the remainder without losing or inventing millicents', () => {
      const total = 100
      const shares = splitMillis(total, 3)
      expect(shares.reduce((a, b) => a + b, 0)).toBe(total)
    })

    it('returns an empty list when there are no recipients', () => {
      expect(splitMillis(100, 0)).toEqual([])
    })

    it('returns the whole total for a single recipient', () => {
      expect(splitMillis(100, 1)).toEqual([100])
    })

    it('returns all-zero shares for a zero total', () => {
      expect(splitMillis(0, 4)).toEqual([0, 0, 0, 0])
    })

    it('handles large totals without drift', () => {
      const total = 1_000_000
      const shares = splitMillis(total, 3)
      expect(shares.reduce((a, b) => a + b, 0)).toBe(total)
    })

    it('gives the extra millicent to the first remainder people', () => {
      expect(splitMillis(101, 3)).toEqual([34, 34, 33])
    })
  })

  describe('rescaleMillis', () => {
    it('preserves ratios when the current total is non-zero', () => {
      expect(rescaleMillis([100, 300], 200)).toEqual([50, 150])
    })

    it('falls back to an equal split when the current total is zero', () => {
      expect(rescaleMillis([0, 0, 0], 100)).toEqual([34, 33, 33])
    })

    it('returns an empty list for an empty input', () => {
      expect(rescaleMillis([], 100)).toEqual([])
    })

    it('scales a single element to the target', () => {
      expect(rescaleMillis([50], 100)).toEqual([100])
    })

    it('sums exactly to the target, distributing the diff to the first elements', () => {
      const target = 100
      const shares = rescaleMillis([1, 2, 3], target)
      expect(shares.reduce((a, b) => a + b, 0)).toBe(target)
      expect(shares).toEqual([17, 33, 50])
    })
  })

  describe('formatAmount', () => {
    it('formats with 2 decimals by default', () => {
      expect(formatAmount(1234.5)).toBe('1,234.50')
    })

    it('shows 3 decimals with fullPrecision when the third decimal is non-zero', () => {
      expect(formatAmount(3.334, { fullPrecision: true })).toBe('3.334')
    })

    it('keeps 2 decimals with fullPrecision when the third decimal is zero', () => {
      expect(formatAmount(3.33, { fullPrecision: true })).toBe('3.33')
    })

    it('formats zero as 0.00', () => {
      expect(formatAmount(0)).toBe('0.00')
    })

    it('accepts string input', () => {
      expect(formatAmount('12.5')).toBe('12.50')
    })
  })

  describe('formatCurrency', () => {
    it('formats with the default EUR symbol', () => {
      expect(formatCurrency(12.5)).toBe('€12.50')
    })

    it('respects the currency option', () => {
      expect(formatCurrency(12.5, { currency: 'USD' })).toBe('$12.50')
    })

    it('shows 3 decimals with fullPrecision when the third decimal is non-zero', () => {
      expect(formatCurrency(3.334, { fullPrecision: true })).toBe('€3.334')
    })
  })
})
