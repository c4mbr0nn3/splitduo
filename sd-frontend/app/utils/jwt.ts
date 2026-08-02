// Decode a JWT's `exp` claim (Unix seconds) without validating the signature.
// Returns null if the token is malformed or has no exp claim. The backend
// remains the source of truth for token validity; this is only for scheduling
// proactive refreshes on the client.

/**
 * Decode the `exp` claim from a JWT without validating the signature.
 * @param token - The JWT string
 * @returns The `exp` value in Unix seconds, or null if unreadable
 */
export function decodeJwtExp(token: string): number | null {
  if (typeof token !== 'string' || token === '') return null
  const parts = token.split('.')
  if (parts.length < 2) return null
  const payloadEncoded = parts[1]
  if (!payloadEncoded) return null
  try {
    // base64url → base64 → JSON
    const b64 = payloadEncoded.replace(/-/g, '+').replace(/_/g, '/')
    const json = decodeURIComponent(
      atob(b64)
        .split('')
        .map(c => `%${(`00${c.charCodeAt(0).toString(16)}`).slice(-2)}`)
        .join(''),
    )
    const payload: Record<string, unknown> = JSON.parse(json)
    const exp = payload?.exp
    return typeof exp === 'number' ? exp : null
  }
  catch {
    return null
  }
}
