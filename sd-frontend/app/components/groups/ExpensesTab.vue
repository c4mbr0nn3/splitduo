<template>
  <div>
    <div class="mb-6">
      <div
        v-if="summary && mySummary"
        class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4 lg:gap-6 items-stretch"
      >
        <GroupsUserBalanceCard
          class="lg:col-span-2"
          :balance="mySummary"
          :is-alias-mode="isAliasMode"
        />
        <GroupsStatsCards
          class="lg:col-span-3"
          :total-expenses="expensePagination.total"
          :group-total="getGroupTotal()"
          :suggestion="mySuggestion"
          :is-alias-mode="isAliasMode"
        />
      </div>
      <UCard
        v-else
        variant="outline"
      >
        <UiEmptyState
          icon="i-lucide-bar-chart-3"
          :title="$t('expenses.noSummaryAvailable')"
          :subtitle="$t('expenses.noSummarySubtitle')"
        />
      </UCard>
    </div>
    <div>
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-lg font-semibold text-primary">
          {{ $t('expenses.title') }}
        </h3>
        <div class="flex items-center gap-2">
          <UButton
            class="md:hidden"
            icon="i-lucide-filter"
            :label="activeFilterCount ? $t('expenses.filtersCount', { count: activeFilterCount }) : $t('expenses.filters')"
            size="sm"
            variant="outline"
            @click="mobileFiltersOpen = !mobileFiltersOpen"
          />
          <template v-if="isAiEnabled">
            <UiScanReceiptButton :group-id="groupId" />
          </template>
          <UButton
            icon="i-lucide-plus"
            size="sm"
            @click="addExpense"
          >
            <span class="hidden sm:inline">{{ $t('expenses.addExpense') }}</span>
          </UButton>
        </div>
      </div>
      <div class="md:grid md:grid-cols-3 md:gap-4">
        <div :class="['md:block', 'md:col-span-1', mobileFiltersOpen ? 'block mb-4' : 'hidden']">
          <GroupsExpenseFilterCard
            v-model:filters="pendingFilters"
            :category-options="categoryOptions"
            :member-options="memberOptions"
            :active-filter-count="activeFilterCount"
            @apply="applyFilters"
            @clear="clearFilters"
          />
        </div>
        <div class="md:col-span-2">
          <div
            v-if="showSkeleton"
            class="space-y-3"
          >
            <GroupsExpenseCardSkeleton
              v-for="i in 4"
              :key="i"
            />
          </div>
          <div v-else-if="expenses.length">
            <div class="space-y-3">
              <GroupsExpenseCard
                v-for="expense in expenses"
                :key="expense.id"
                :expense="expense"
                :current-user="user"
                @expense-deleted="onExpenseDeleted"
              />
            </div>
          </div>
          <UiEmptyState
            v-else
            icon="i-lucide-receipt"
            :title="$t('expenses.noExpensesFound')"
            :subtitle="$t('expenses.noExpensesSubtitle')"
          />
        </div>
      </div>
    </div>
    <template v-if="expensePagination.totalPages > 1">
      <USeparator class="mt-4" />
      <div class="flex justify-center mt-4">
        <UPagination
          v-model:page="currentPage"
          :items-per-page="expensePagination.limit"
          :total="expensePagination.total"
          :sibling-count="1"
        />
      </div>
    </template>
  </div>
</template>

<script setup>
const { t } = useI18n()
const props = defineProps({
  groupId: { type: String, required: true },
})

const route = useRoute()
const router = useRouter()
const { user } = useAuth()
const { expenses, fetchExpenses, pagination: expensePagination } = useExpenses(props.groupId)
const { balanceSummary, fetchBalanceSummary, isAliasMode, fetchGroup } = useBalances(props.groupId)
const { aliases, fetchAliases } = useAliases()
const { categories } = useCategories()
const { isAiEnabled } = useAiStatus()

const currentPage = ref(Number(route.query.page) || 1)
const showSkeleton = ref(true)
const mobileFiltersOpen = ref(false)
const pendingFilters = ref({
  startDate: route.query.startDate || null,
  endDate: route.query.endDate || null,
  category: route.query.category || null,
  userId: route.query.userId || null,
  search: route.query.search || null,
})
const activeFilters = ref({ ...pendingFilters.value })

const buildQuery = (filters, page) => {
  const q = { tab: 'expenses', page }
  if (filters.startDate) q.startDate = filters.startDate
  if (filters.endDate) q.endDate = filters.endDate
  if (filters.category) q.category = filters.category
  if (filters.userId) q.userId = filters.userId
  if (filters.search) q.search = filters.search
  return q
}
const activeFilterCount = computed(() => Object.values(activeFilters.value).filter(Boolean).length)

const summary = computed(() => balanceSummary.value)

const currentUserAliasId = computed(() => {
  if (!isAliasMode.value || !user.value?.id) return null
  const userAlias = aliases.value.find(a => a.members?.some(m => m.id === user.value.id))
  return userAlias?.id || null
})

const mySummary = computed(() => {
  if (!summary.value) return null

  if (isAliasMode.value) {
    const aliasId = currentUserAliasId.value
    if (!aliasId) return null
    const my = summary.value.balances.find(el => el.aliasId === aliasId)
    return my
      ? {
          balance: my.balance || 0,
          totalPaid: my.totalPaid || 0,
          totalOwed: my.totalOwed || 0,
          aliasName: my.aliasName,
        }
      : null
  }

  const my = summary.value.balances.find(el => el.userId === user.value?.id)
  return my
    ? {
        balance: my.balance || 0,
        totalPaid: my.totalPaid || 0,
        totalOwed: my.totalOwed || 0,
      }
    : null
})

const userMap = computed(() => {
  if (!summary.value?.balances) return {}
  return Object.fromEntries(summary.value.balances.map(b => [b.userId, b.user]))
})

const mySuggestion = computed(() => {
  if (!summary.value?.suggestions?.length) return null

  if (isAliasMode.value) {
    const aliasId = currentUserAliasId.value
    if (!aliasId) return null
    const s = summary.value.suggestions.find(el => el.fromAliasId === aliasId || el.toAliasId === aliasId)
    if (!s) return null
    return {
      label: s.fromAliasId === aliasId
        ? `${t('stats.yourAlias', { name: s.fromAliasName })} ${t('stats.owes')} ${s.toAliasName} ${formatAmount(s.amount)} €`
        : `${s.fromAliasName} ${t('stats.owes')} ${t('stats.yourAlias', { name: s.toAliasName })} ${formatAmount(s.amount)} €`,
      isOwed: s.toAliasId === aliasId,
    }
  }

  if (!user.value?.id) return null
  const id = user.value.id
  const s = summary.value.suggestions.find(el => el.fromUserId === id || el.toUserId === id)
  if (!s) return null
  const from = userMap.value[s.fromUserId]
  const to = userMap.value[s.toUserId]
  return {
    label: s.fromUserId === id
      ? `${t('stats.youOwe')} ${to?.firstName || 'someone'} ${formatAmount(s.amount)} €`
      : `${from?.firstName || 'someone'} ${t('stats.owes')} ${t('stats.you')} ${formatAmount(s.amount)} €`,
    isOwed: s.toUserId === id,
  }
})

const categoryOptions = computed(() => [
  { value: null, label: t('expenses.allCategories') },
  ...categories.value.map(c => ({ value: c.name, label: c.name })),
])

const memberOptions = computed(() => {
  if (!summary.value?.balances) return [{ value: null, label: t('expenses.allMembers') }]
  if (isAliasMode.value) {
    return [
      { value: null, label: t('expenses.allAliases') },
      ...summary.value.balances.map(b => ({
        value: b.aliasId,
        label: b.aliasName,
      })),
    ]
  }
  return [
    { value: null, label: t('expenses.allMembers') },
    ...summary.value.balances.map(b => ({
      value: b.userId,
      label: `${b.user.firstName} ${b.user.lastName}`,
    })),
  ]
})

const getGroupTotal = () => {
  if (!summary.value?.balances) return 0
  return summary.value.balances.reduce((total, balance) => total + balance.totalPaid, 0)
}

const addExpense = () => {
  navigateTo(`/expenses/add?groupId=${props.groupId}`)
}

const applyFilters = async () => {
  activeFilters.value = { ...pendingFilters.value }
  currentPage.value = 1
  router.push({ query: buildQuery(activeFilters.value, 1) })
  await fetchExpenses({ ...activeFilters.value, page: 1 })
  mobileFiltersOpen.value = false
}

const clearFilters = async () => {
  const empty = { startDate: null, endDate: null, category: null, userId: null, search: null }
  pendingFilters.value = { ...empty }
  activeFilters.value = { ...empty }
  currentPage.value = 1
  router.push({ query: { tab: 'expenses', page: 1 } })
  await fetchExpenses({ page: 1 })
  mobileFiltersOpen.value = false
}

const onExpenseDeleted = async () => {
  await Promise.all([
    fetchExpenses({ ...activeFilters.value, page: currentPage.value }),
    fetchBalanceSummary(),
  ])
}

watch(currentPage, async (newPage) => {
  router.push({ query: buildQuery(activeFilters.value, newPage) })
  await fetchExpenses({ ...activeFilters.value, page: newPage })
}, { immediate: false })

onMounted(async () => {
  try {
    await withMinDuration(async () => {
      await Promise.all([
        fetchGroup(props.groupId),
        fetchExpenses({ ...activeFilters.value, page: currentPage.value }),
      ])
      if (isAliasMode.value) {
        await fetchAliases(props.groupId)
      }
      await fetchBalanceSummary()
    })
  }
  finally {
    showSkeleton.value = false
  }
})
</script>
