export async function withMinDuration(fn, ms = 300) {
  const start = Date.now()
  await fn()
  const remaining = ms - (Date.now() - start)
  if (remaining > 0) await new Promise(r => setTimeout(r, remaining))
}
