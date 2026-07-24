// Integer-millicent helpers: 3-decimal currency math without FP drift.
// Every balance comparison in the app runs on millis (integers) and converts
// back to floats only when the value is assigned to a model or displayed.

/**
 * Convert a currency amount to integer millicents (thousandths).
 * @param {number|string} n
 * @returns {number} e.g. 3.334 → 3334, "10.1" → 10100
 */
export const toMillis = n => Math.round((Number(n) || 0) * 1000)

/**
 * Convert integer millicents back to a decimal amount.
 * @param {number} m
 * @returns {number} e.g. 3334 → 3.334
 */
export const fromMillis = m => m / 1000

/**
 * Split a total (in millis) fairly across n recipients: one extra millicent
 * per person until the remainder is absorbed, never dumping it all on the first.
 *
 * @param {number} totalMillis
 * @param {number} n
 * @returns {number[]} shares in millis, sums exactly to totalMillis
 */
export const splitMillis = (totalMillis, n) => {
  if (n <= 0) return []
  const base = Math.floor(totalMillis / n)
  const remainder = totalMillis - base * n
  return Array.from({ length: n }, (_, i) => base + (i < remainder ? 1 : 0))
}

/**
 * Rescale a list of amounts (in millis) to sum to targetMillis while preserving
 * ratios. Falls back to an equal split when the current total is zero.
 *
 * @param {number[]} currentMillisList
 * @param {number} targetMillis
 * @returns {number[]} rescaled shares in millis, sums exactly to targetMillis
 */
export const rescaleMillis = (currentMillisList, targetMillis) => {
  const n = currentMillisList.length
  if (n === 0) return []
  const currentTotal = currentMillisList.reduce((s, m) => s + m, 0)
  if (currentTotal === 0) return splitMillis(targetMillis, n)
  const scaled = currentMillisList.map(m =>
    Math.floor((m * targetMillis) / currentTotal),
  )
  let diff = targetMillis - scaled.reduce((s, m) => s + m, 0)
  for (let i = 0; diff > 0 && i < scaled.length; i++, diff--) {
    scaled[i] += 1
  }
  return scaled
}

/**
 * Format an amount as a plain number, rounded to 2 decimals for display.
 * Pass `{ fullPrecision: true }` to show 3 decimals when the third decimal is
 * non-zero (used in the expense form where exact millicent values matter).
 *
 * Used for display components that add their own currency symbol around the value.
 *
 * @param {number|string} amount
 * @param {{ fullPrecision?: boolean }} [options]
 * @returns {string} e.g. "12.50", "3.334"
 */
export function formatAmount(amount, { fullPrecision = false } = {}) {
  const n = Number(amount) || 0
  const needs3 = fullPrecision && Math.round(n * 1000) % 10 !== 0
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: needs3 ? 3 : 2,
  }).format(n)
}

/**
 * Format an amount as a full currency string with symbol, rounded to 2 decimals
 * for display. Pass `{ fullPrecision: true }` to show 3 decimals when the third
 * decimal is non-zero (used in the expense form).
 *
 * @param {number|string} amount
 * @param {{ currency?: string, fullPrecision?: boolean }} [options] - defaults to EUR
 * @returns {string} e.g. "€12.50", "€3.334"
 */
export function formatCurrency(amount, { currency = 'EUR', fullPrecision = false } = {}) {
  const n = Number(amount) || 0
  const needs3 = fullPrecision && Math.round(n * 1000) % 10 !== 0
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: needs3 ? 3 : 2,
  }).format(n)
}
