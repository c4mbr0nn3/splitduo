import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref } from 'vue'

import useBalances from './useBalances'
import { apiMock } from '~/composables/api/base.mock'
import type {
  Group,
  GroupStats,
  NormalBalance,
  AliasBalance,
  BalanceSummary,
  AliasBalanceSummary,
} from '~/types/domain'

// --- Hoisted mocks (referenced by the vi.mock factories below) ---
const tMock = vi.hoisted(() => vi.fn((key: string) => key))
const notificationsMock = vi.hoisted(() => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))
const groupsMock = vi.hoisted(() => ({
  fetchGroup: vi.fn(),
  currentGroup: { value: null } as { value: Group | null },
}))

// useApi / useNotifications are auto-imported inside useBalances.ts; mock the
// composable modules so every API call and toast is controlled from the test.
// useI18n is auto-imported from 'vue-i18n' (see .nuxt/imports.d.ts); mock the
// module so `t` is a controllable passthrough that returns the message key.
// useGroups is auto-imported inside useBalances.ts; mock it so currentGroup
// (and therefore isAliasMode) is controllable from the test.
vi.mock('~/composables/api/base', () => ({ default: () => apiMock }))
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: tMock }) }))
vi.mock('~/composables/utils/useNotifications', () => ({ default: () => notificationsMock }))
vi.mock('~/composables/resources/useGroups', () => {
  const currentGroup = ref<Group | null>(null)
  groupsMock.currentGroup = currentGroup
  return { default: () => ({ fetchGroup: groupsMock.fetchGroup, currentGroup }) }
})

const group = (overrides: Partial<Group> = {}): Group => ({
  id: 'group-1',
  name: 'Trip to Rome',
  createdByUserId: 'user-1',
  memberCount: 2,
  createdAt: 0,
  updatedAt: 0,
  netBalance: 0,
  useAliases: false,
  aliasSetupFinalized: false,
  ...overrides,
})

const normalBalance = (overrides: Partial<NormalBalance> = {}): NormalBalance => ({
  userId: 'user-1',
  user: { id: 'user-1', firstName: 'Alice' },
  balance: 10,
  totalPaid: 20,
  totalOwed: 10,
  ...overrides,
})

const aliasBalance = (overrides: Partial<AliasBalance> = {}): AliasBalance => ({
  aliasId: 'alias-1',
  aliasName: 'Family',
  balance: 10,
  totalPaid: 20,
  totalOwed: 10,
  members: [{ id: 'user-1', firstName: 'Alice' }],
  isSingleton: false,
  ...overrides,
})

const balanceSummary = (overrides: Partial<BalanceSummary> = {}): BalanceSummary => ({
  groupId: 'group-1',
  balances: [normalBalance()],
  suggestions: [{ fromUserId: 'user-1', toUserId: 'user-2', amount: 5, description: 'Pay Bob' }],
  ...overrides,
})

const aliasBalanceSummary = (overrides: Partial<AliasBalanceSummary> = {}): AliasBalanceSummary => ({
  groupId: 'group-1',
  balances: [aliasBalance()],
  suggestions: [{
    fromAliasId: 'alias-1',
    toAliasId: 'alias-2',
    fromAliasName: 'Family',
    toAliasName: 'Friends',
    amount: 5,
    description: 'Pay Friends',
  }],
  ...overrides,
})

const groupStats = (overrides: Partial<GroupStats> = {}): GroupStats => ({
  totalExpenses: 5,
  totalAmount: 100,
  balances: [normalBalance()],
  categoryBreakdown: [],
  monthlyBreakdown: [],
  ...overrides,
})

describe('useBalances', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    groupsMock.currentGroup.value = null
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('fetchBalances', () => {
    it('stores the balances as-is in normal mode and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: [normalBalance()] })
      const balances = useBalances('group-1')

      await balances.fetchBalances()

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/balances')
      expect(balances.balances.value).toEqual([normalBalance()])
      expect(balances.isLoading.value).toBe(false)
    })

    it('normalizes balances with alias metadata in alias mode', async () => {
      groupsMock.currentGroup.value = group({ useAliases: true })
      apiMock.get.mockResolvedValue({ success: true, data: [aliasBalance()] })
      const balances = useBalances('group-1')

      await balances.fetchBalances()

      expect(balances.balances.value).toEqual([aliasBalance()])
    })

    it('defaults members to an empty array for non-alias balances in alias mode', async () => {
      groupsMock.currentGroup.value = group({ useAliases: true })
      apiMock.get.mockResolvedValue({ success: true, data: [normalBalance()] })
      const balances = useBalances('group-1')

      await balances.fetchBalances()

      const normalized = balances.balances.value[0]
      if (!normalized || !('aliasId' in normalized)) throw new Error('expected an alias-normalized balance')
      expect(normalized.aliasId).toBeUndefined()
      expect(normalized.aliasName).toBeUndefined()
      expect(normalized.members).toEqual([])
      expect(normalized.isSingleton).toBeUndefined()
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const balances = useBalances('')

      await balances.fetchBalances()

      expect(apiMock.get).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Network down'))
      const balances = useBalances('group-1')

      await expect(balances.fetchBalances()).rejects.toThrow('Network down')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.balances.loadFailed')
      expect(balances.isLoading.value).toBe(false)
    })
  })

  describe('fetchBalanceSummary', () => {
    it('stores the summary as-is in normal mode and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: balanceSummary() })
      const balances = useBalances('group-1')

      await balances.fetchBalanceSummary()

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/balances/summary')
      expect(balances.balanceSummary.value).toEqual(balanceSummary())
      expect(balances.isLoading.value).toBe(false)
    })

    it('normalizes the summary with alias balances and suggestions in alias mode', async () => {
      groupsMock.currentGroup.value = group({ useAliases: true })
      apiMock.get.mockResolvedValue({ success: true, data: aliasBalanceSummary() })
      const balances = useBalances('group-1')

      await balances.fetchBalanceSummary()

      expect(balances.balanceSummary.value).toEqual(aliasBalanceSummary())
    })

    it('defaults balances and suggestions to empty arrays when absent in alias mode', async () => {
      groupsMock.currentGroup.value = group({ useAliases: true })
      apiMock.get.mockResolvedValue({ success: true, data: { groupId: 'group-1' } })
      const balances = useBalances('group-1')

      await balances.fetchBalanceSummary()

      expect(balances.balanceSummary.value).toEqual({ groupId: 'group-1', balances: [], suggestions: [] })
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const balances = useBalances('')

      await balances.fetchBalanceSummary()

      expect(apiMock.get).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Summary failed'))
      const balances = useBalances('group-1')

      await expect(balances.fetchBalanceSummary()).rejects.toThrow('Summary failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.balances.summaryLoadFailed')
      expect(balances.isLoading.value).toBe(false)
    })
  })

  describe('fetchGroupStats', () => {
    it('stores the stats as-is in normal mode and clears isLoading', async () => {
      apiMock.get.mockResolvedValue({ success: true, data: groupStats() })
      const balances = useBalances('group-1')

      await balances.fetchGroupStats()

      expect(apiMock.get).toHaveBeenCalledWith('/groups/group-1/stats')
      expect(balances.groupStats.value).toEqual(groupStats())
      expect(balances.isLoading.value).toBe(false)
    })

    it('normalizes the stats balances with alias metadata in alias mode', async () => {
      groupsMock.currentGroup.value = group({ useAliases: true })
      apiMock.get.mockResolvedValue({ success: true, data: groupStats({ balances: [aliasBalance()] }) })
      const balances = useBalances('group-1')

      await balances.fetchGroupStats()

      expect(balances.groupStats.value?.balances).toEqual([aliasBalance()])
    })

    it('defaults members to an empty array for non-alias balances in alias mode', async () => {
      groupsMock.currentGroup.value = group({ useAliases: true })
      apiMock.get.mockResolvedValue({ success: true, data: groupStats() })
      const balances = useBalances('group-1')

      await balances.fetchGroupStats()

      // GroupStats.balances is typed as BalanceDto[]; the composable casts to
      // AliasBalance during normalization (same cast as in useBalances.ts).
      const normalized = balances.groupStats.value?.balances[0] as AliasBalance | undefined
      if (!normalized) throw new Error('expected a normalized stat balance')
      expect(normalized.aliasId).toBeUndefined()
      expect(normalized.members).toEqual([])
    })

    it('returns early without calling the API when groupId is empty', async () => {
      const balances = useBalances('')

      await balances.fetchGroupStats()

      expect(apiMock.get).not.toHaveBeenCalled()
    })

    it('shows an error toast, re-throws, and clears isLoading when the API call fails', async () => {
      apiMock.get.mockRejectedValue(new Error('Stats failed'))
      const balances = useBalances('group-1')

      await expect(balances.fetchGroupStats()).rejects.toThrow('Stats failed')

      expect(notificationsMock.showError).toHaveBeenCalledWith('toasts.balances.statsLoadFailed')
      expect(balances.isLoading.value).toBe(false)
    })
  })

  describe('isAliasMode', () => {
    it('is false when no group is loaded', () => {
      const balances = useBalances('group-1')

      expect(balances.isAliasMode.value).toBe(false)
    })

    it('is true when the current group uses aliases', () => {
      groupsMock.currentGroup.value = group({ useAliases: true })
      const balances = useBalances('group-1')

      expect(balances.isAliasMode.value).toBe(true)
    })

    it('is false when the current group does not use aliases', () => {
      groupsMock.currentGroup.value = group({ useAliases: false })
      const balances = useBalances('group-1')

      expect(balances.isAliasMode.value).toBe(false)
    })
  })

  describe('group', () => {
    it('exposes the current group from useGroups', () => {
      groupsMock.currentGroup.value = group()
      const balances = useBalances('group-1')

      expect(balances.group.value).toEqual(group())
    })
  })

  describe('fetchGroup', () => {
    it('delegates to useGroups.fetchGroup', async () => {
      groupsMock.fetchGroup.mockResolvedValue(group())
      const balances = useBalances('group-1')

      const result = await balances.fetchGroup('group-1')

      expect(groupsMock.fetchGroup).toHaveBeenCalledWith('group-1')
      expect(result).toEqual(group())
    })
  })
})
