import { describe, it, expect } from 'vitest'
import { buildSettleUpUrl } from './settle'

describe('buildSettleUpUrl', () => {
  it('returns a confirm URL with from/to/amount when the user is the payer (individual mode)', () => {
    const url = buildSettleUpUrl({
      groupId: 'g1',
      suggestions: [{ fromUserId: 'u1', toUserId: 'u2', amount: 500, description: 'x' }],
      isAliasMode: false,
      currentUserId: 'u1',
      currentAliasId: null,
    })
    expect(url).toBe('/groups/g1/settle/confirm?from=u1&to=u2&amount=500')
  })

  it('falls back to the settle page when the user is only a creditor (toUserId)', () => {
    const url = buildSettleUpUrl({
      groupId: 'g1',
      suggestions: [{ fromUserId: 'u2', toUserId: 'u1', amount: 500, description: 'x' }],
      isAliasMode: false,
      currentUserId: 'u1',
      currentAliasId: null,
    })
    expect(url).toBe('/groups/g1/settle')
  })

  it('falls back to the settle page when there are no suggestions', () => {
    const url = buildSettleUpUrl({
      groupId: 'g1',
      suggestions: [],
      isAliasMode: false,
      currentUserId: 'u1',
      currentAliasId: null,
    })
    expect(url).toBe('/groups/g1/settle')
  })

  it('returns a confirm URL with fromAlias/toAlias/amount in alias mode when currentAliasId matches', () => {
    const url = buildSettleUpUrl({
      groupId: 'g1',
      suggestions: [{ fromAliasId: 'a1', toAliasId: 'a2', fromAliasName: 'a1', toAliasName: 'a2', amount: 750, description: 'x' }],
      isAliasMode: true,
      currentUserId: 'u1',
      currentAliasId: 'a1',
    })
    expect(url).toBe('/groups/g1/settle/confirm?fromAlias=a1&toAlias=a2&amount=750')
  })

  it('falls back to the settle page in alias mode when currentAliasId is null', () => {
    const url = buildSettleUpUrl({
      groupId: 'g1',
      suggestions: [{ fromAliasId: 'a1', toAliasId: 'a2', fromAliasName: 'a1', toAliasName: 'a2', amount: 750, description: 'x' }],
      isAliasMode: true,
      currentUserId: 'u1',
      currentAliasId: null,
    })
    expect(url).toBe('/groups/g1/settle')
  })

  it('uses the first payer-side suggestion when there are multiple', () => {
    const url = buildSettleUpUrl({
      groupId: 'g1',
      suggestions: [
        { fromUserId: 'u1', toUserId: 'u2', amount: 100, description: 'x' },
        { fromUserId: 'u1', toUserId: 'u3', amount: 900, description: 'x' },
      ],
      isAliasMode: false,
      currentUserId: 'u1',
      currentAliasId: null,
    })
    expect(url).toBe('/groups/g1/settle/confirm?from=u1&to=u2&amount=100')
  })
})
