import { useNuxtApp } from '#app/nuxt'

// Integer-millicent helpers: 3-decimal currency math without FP drift.
// Every balance comparison in the app runs on millis (integers) and converts
// back to floats only when the value is assigned to a model or displayed.

/**
 * Convert a currency amount to integer millicents (thousandths).
 * @param n - The amount to convert (e.g., 3.334 → 3334, "10.1" → 10100)
 * @returns Integer millicents
 */
export const toMillis = (n: number | string): number => Math.round((Number(n) || 0) * 1000)

/**
 * Convert integer millicents back to a decimal amount.
 * @param m - Millicents (e.g., 3334 → 3.334)
 * @returns Decimal amount
 */
export const fromMillis = (m: number): number => m / 1000

/**
 * Split a total (in millis) fairly across n recipients: one extra millicent
 * per person until the remainder is absorbed, never dumping it all on the first.
 *
 * @param totalMillis - Total amount in millicents
 * @param n - Number of recipients
 * @returns Shares in millis, sums exactly to totalMillis
 */
export const splitMillis = (totalMillis: number, n: number): number[] => {
  if (n <= 0) return []
  const base = Math.floor(totalMillis / n)
  const remainder = totalMillis - base * n
  return Array.from({ length: n }, (_, i) => base + (i < remainder ? 1 : 0))
}

/**
 * Rescale a list of amounts (in millis) to sum to targetMillis while preserving
 * ratios. Falls back to an equal split when the current total is zero.
 *
 * @param currentMillisList - Current amounts in millicents
 * @param targetMillis - Target total in millicents
 * @returns Rescaled shares in millis, sums exactly to targetMillis
 */
export const rescaleMillis = (currentMillisList: number[], targetMillis: number): number[] => {
  const n = currentMillisList.length
  if (n === 0) return []
  const currentTotal = currentMillisList.reduce((s, m) => s + m, 0)
  if (currentTotal === 0) return splitMillis(targetMillis, n)
  const scaled = currentMillisList.map(m =>
    Math.floor((m * targetMillis) / currentTotal),
  )
  let diff = targetMillis - scaled.reduce((s, m) => s + m, 0)
  for (let i = 0; diff > 0 && i < scaled.length; i++, diff--) {
    scaled[i]! += 1
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
 * @param amount - The amount to format
 * @param options - Formatting options
 * @returns Formatted number string (e.g., "12.50", "3.334")
 */
export function formatAmount(amount: number | string, { fullPrecision = false }: { fullPrecision?: boolean } = {}): string {
  const n = Number(amount) || 0
  const needs3 = fullPrecision && Math.round(n * 1000) % 10 !== 0
  const { $i18n } = useNuxtApp()
  return new Intl.NumberFormat($i18n.locale.value, {
    minimumFractionDigits: 2,
    maximumFractionDigits: needs3 ? 3 : 2,
  }).format(n)
}

/**
 * Format an amount as a full currency string with symbol, rounded to 2 decimals
 * for display. Pass `{ fullPrecision: true }` to show 3 decimals when the third
 * decimal is non-zero (used in the expense form).
 *
 * @param amount - The amount to format
 * @param options - Formatting options (defaults to EUR)
 * @returns Formatted currency string (e.g., "€12.50", "€3.334")
 */
export function formatCurrency(amount: number | string, { currency = 'EUR', fullPrecision = false }: { currency?: string, fullPrecision?: boolean } = {}): string {
  const n = Number(amount) || 0
  const needs3 = fullPrecision && Math.round(n * 1000) % 10 !== 0
  const { $i18n } = useNuxtApp()
  return new Intl.NumberFormat($i18n.locale.value, {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: needs3 ? 3 : 2,
  }).format(n)
}

/**
 * Distribute a total (in millicents) across participants by their percentage
 * shares using floor + largest-remainder (Hamilton) so shares sum exactly to
 * the total. Callers must ensure percentages sum to 100; the function does
 * not renormalize.
 *
 * @param percentages - Per-participant percentages (e.g. [33.33, 33.33, 33.34])
 * @param totalMillis - Total amount in millicents
 * @returns Shares in millicents, sums exactly to totalMillis
 */
export const distributeByPercentages = (percentages: number[], totalMillis: number): number[] => {
  if (percentages.length === 0 || totalMillis === 0) return percentages.map(() => 0)
  const shares = new Array<number>(percentages.length).fill(0)
  const remainders = new Array<number>(percentages.length).fill(0)
  let distributed = 0
  for (let i = 0; i < percentages.length; i++) {
    const exact = (percentages[i]! / 100) * totalMillis
    shares[i] = Math.floor(exact)
    remainders[i] = exact - shares[i]!
    distributed += shares[i]!
  }
  let rest = totalMillis - distributed
  while (rest > 0) {
    let pick = -1
    for (let i = 0; i < remainders.length; i++) {
      if (remainders[i]! > 0 && (pick < 0 || remainders[i]! > remainders[pick]!)) pick = i
    }
    if (pick < 0) break
    shares[pick]!++
    remainders[pick] = 0
    rest--
  }
  return shares
}

/**
 * Convert per-participant amounts (in millicents) back to 2dp percentages.
 * The first participant absorbs the 2dp residual so the sum is always 100.00.
 *
 * @param amountsMillis - Per-participant amounts in millicents
 * @param totalMillis - Total amount in millicents
 * @returns Percentages (2dp), sums to 100.00
 */
export const amountsToPercentages = (amountsMillis: number[], totalMillis: number): number[] => {
  if (amountsMillis.length === 0 || totalMillis === 0) return amountsMillis.map(() => 0)
  const percentages = amountsMillis.map(m => Math.round((m / totalMillis) * 10000) / 100)
  // First participant absorbs residual so sum is exactly 100.00
  const sum = percentages.reduce((s, p) => s + p, 0)
  const residual = Math.round((100 - sum) * 100) / 100
  if (percentages.length > 0) {
    percentages[0] = Math.round((percentages[0]! + residual) * 100) / 100
  }
  return percentages
}

/**
 * Generate equal 2dp percentages for n participants, with the first absorbing
 * the residual so the sum is always 100.00.
 *
 * @param n - Number of participants
 * @returns Percentages (2dp), sums to 100.00
 */
export const equalPercentages = (n: number): number[] => {
  if (n <= 0) return []
  const base = Math.floor(10000 / n) / 100
  const percentages = new Array<number>(n).fill(base)
  const sum = base * n
  const residual = Math.round((100 - sum) * 100) / 100
  percentages[0] = Math.round((percentages[0]! + residual) * 100) / 100
  return percentages
}
