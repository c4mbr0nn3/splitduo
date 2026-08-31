import type { BalanceSuggestion, AliasSettlementSuggestion } from '~/types/domain'

/**
 * Build the settle-confirm URL for the first suggestion where the current user
 * is the payer ("from" side). Returns the group's settle page URL when the user
 * has nothing to pay (fallback target shows full suggestions + manual entry).
 */
export function buildSettleUpUrl(options: {
  groupId: string
  suggestions: BalanceSuggestion[] | AliasSettlementSuggestion[]
  isAliasMode: boolean
  currentUserId: string
  currentAliasId: string | null
}): string {
  const { groupId, suggestions, isAliasMode, currentUserId, currentAliasId } = options

  if (isAliasMode) {
    const hit = (suggestions as AliasSettlementSuggestion[]).find(
      s => 'fromAliasId' in s && s.fromAliasId === currentAliasId,
    )
    if (hit) {
      return `/groups/${groupId}/settle/confirm?fromAlias=${hit.fromAliasId}&toAlias=${hit.toAliasId}&amount=${hit.amount}`
    }
  }
  else {
    const hit = (suggestions as BalanceSuggestion[]).find(
      s => !('fromAliasId' in s) && s.fromUserId === currentUserId,
    )
    if (hit) {
      return `/groups/${groupId}/settle/confirm?from=${hit.fromUserId}&to=${hit.toUserId}&amount=${hit.amount}`
    }
  }

  return `/groups/${groupId}/settle`
}
