/**
 * Wraps an async function to ensure it takes at least `ms` milliseconds.
 * Useful for preventing flash-of-loading states on fast operations.
 *
 * @param fn - The async function to wrap
 * @param ms - Minimum duration in milliseconds (default: 300)
 * @returns The result of the wrapped function
 */
export async function withMinDuration<T>(fn: () => Promise<T>, ms: number = 300): Promise<T> {
  const start = Date.now()
  const result = await fn()
  const remaining = ms - (Date.now() - start)
  if (remaining > 0) await new Promise(resolve => setTimeout(resolve, remaining))
  return result
}
