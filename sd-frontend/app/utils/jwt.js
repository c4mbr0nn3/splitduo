// Decode a JWT's `exp` claim (Unix seconds) without validating the signature.
// Returns null if the token is malformed or has no exp claim. The backend
// remains the source of truth for token validity; this is only for scheduling
// proactive refreshes on the client.
export function decodeJwtExp(token) {
  if (typeof token !== 'string' || token === '') return null
  const parts = token.split('.')
  if (parts.length < 2) return null
  try {
    // base64url → base64 → JSON
    const b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const json = decodeURIComponent(
      atob(b64)
        .split('')
        .map(c => `%${(`00${c.charCodeAt(0).toString(16)}`).slice(-2)}`)
        .join(''),
    )
    const payload = JSON.parse(json)
    const exp = payload?.exp
    return typeof exp === 'number' ? exp : null
  }
  catch {
    return null
  }
}
