import { describe, expect, it } from 'vitest'
import { escapeHtml } from './html'

describe('escapeHtml', () => {
  it('leaves names without special characters unchanged', () => {
    expect(escapeHtml('José')).toBe('José')
  })

  it('preserves apostrophes when the escaped string is rendered as HTML', () => {
    const container = document.createElement('div')
    container.innerHTML = escapeHtml('José O\'Neill')
    expect(container.textContent).toBe('José O\'Neill')
  })

  it('neutralizes the SEC-02 payload', () => {
    const escaped = escapeHtml(`<img src=x onerror="document.title='xss'">`)
    expect(escaped).not.toContain('<')
    expect(escaped).not.toContain('>')
    expect(escaped).toContain('&lt;img')
  })

  it('escapes ampersands before entity-looking input', () => {
    expect(escapeHtml('&lt;')).toBe('&amp;lt;')
  })

  it('renders null and undefined as an empty string', () => {
    expect(escapeHtml(null)).toBe('')
    expect(escapeHtml(undefined)).toBe('')
  })

  it('stringifies numeric labels unchanged', () => {
    expect(escapeHtml(42)).toBe('42')
  })
})
