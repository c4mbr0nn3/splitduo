import { describe, it, expect } from 'vitest'
import { decodeJwtExp } from './jwt'

function base64UrlEncode(value: Record<string, unknown>): string {
  return btoa(JSON.stringify(value))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

function makeToken(payload: Record<string, unknown>): string {
  const header = base64UrlEncode({ alg: 'none', typ: 'JWT' })
  return `${header}.${base64UrlEncode(payload)}.signature`
}

describe('decodeJwtExp', () => {
  it('returns the exp claim from a valid token', () => {
    const token = makeToken({ sub: '1', exp: 1700000000 })
    expect(decodeJwtExp(token)).toBe(1700000000)
  })

  it('returns null when the token has no exp claim', () => {
    const token = makeToken({ sub: '1' })
    expect(decodeJwtExp(token)).toBeNull()
  })

  it('returns null when exp is not a number', () => {
    const token = makeToken({ exp: '1700000000' })
    expect(decodeJwtExp(token)).toBeNull()
  })

  it('returns null for an empty string', () => {
    expect(decodeJwtExp('')).toBeNull()
  })

  it('returns null for a token without a payload segment', () => {
    expect(decodeJwtExp('only-header')).toBeNull()
  })

  it('returns null for a payload that is not valid base64', () => {
    expect(decodeJwtExp('header.%%%not-base64%%%.signature')).toBeNull()
  })

  it('returns null for a payload that is not valid JSON', () => {
    const b64 = btoa('not json').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
    expect(decodeJwtExp(`header.${b64}.signature`)).toBeNull()
  })
})
