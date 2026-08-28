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
          :expense-count="Number(expensePagination.total)"
          :group-total="getGroupTotal()"
          :group-id="props.groupId"
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
                :current-user-alias-id="currentUserAliasId"
                :aliases="aliases"
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
    <template v-if="Number(expensePagination.totalPages) > 1">
      <USeparator class="mt-4" />
      <div class="flex justify-center mt-4">
        <UPagination
          v-model:page="currentPage"
          :items-per-page="Number(expensePagination.limit)"
          :total="Number(expensePagination.total)"
          :sibling-count="1"
        />
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import type { Expense, AliasBalance, NormalBalance } from '~/types/domain'
import type { ExpenseFilters } from '~/composables/resources/useExpenses'

const { t } = useI18n()

interface Props {
  groupId: string
}
const props = defineProps<Props>()

const route = useRoute()
const router = useRouter()
const { user } = useAuth()
const { expenses: rawExpenses, fetchExpenses, pagination: expensePagination } = useExpenses(props.groupId)
const expenses = computed(() => rawExpenses.value as unknown as Expense[])
const { balanceSummary, fetchBalanceSummary, isAliasMode, fetchGroup } = useBalances(props.groupId)
const { aliases, fetchAliases } = useAliases()
const { categories } = useCategories()
const { isAiEnabled } = useAiStatus()

const currentPage = ref(Number(route.query.page) || 1)
const showSkeleton = ref(true)
const mobileFiltersOpen = ref(false)
const pendingFilters = ref<ExpenseFilters>({
  startDate: typeof route.query.startDate === 'string' ? route.query.startDate : undefined,
  endDate: typeof route.query.endDate === 'string' ? route.query.endDate : undefined,
  category: typeof route.query.category === 'string' ? route.query.category : undefined,
  userId: typeof route.query.userId === 'string' ? route.query.userId : undefined,
  search: typeof route.query.search === 'string' ? route.query.search : undefined,
})
const activeFilters = ref<ExpenseFilters>({ ...pendingFilters.value })

const buildQuery = (filters: ExpenseFilters, page: number): Record<string, string | number> => {
  const q: Record<string, string | number> = { tab: 'expenses', page }
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
  const userAlias = aliases.value.find(a => a.members?.some(m => m.id === user.value!.id))
  return userAlias?.id || null
})

const mySummary = computed(() => {
  if (!summary.value) return null

  if (isAliasMode.value) {
    const aliasId = currentUserAliasId.value
    if (!aliasId) return null
    const aliasBalances = summary.value.balances as AliasBalance[]
    const my = aliasBalances.find(el => el.aliasId === aliasId)
    return my
      ? {
          balance: Number(my.balance) || 0,
          totalPaid: Number(my.totalPaid) || 0,
          totalOwed: Number(my.totalOwed) || 0,
          aliasName: my.aliasName,
        }
      : null
  }

  const normalBalances = summary.value.balances as NormalBalance[]
  const my = normalBalances.find(el => el.userId === user.value?.id)
  return my
    ? {
        balance: Number(my.balance) || 0,
        totalPaid: Number(my.totalPaid) || 0,
        totalOwed: Number(my.totalOwed) || 0,
      }
    : null
})

const categoryOptions = computed(() => [
  { value: null, label: t('expenses.allCategories') },
  ...categories.value.map(c => ({ value: c.name, label: c.name })),
])

const memberOptions = computed(() => {
  if (!summary.value?.balances) return [{ value: null, label: t('expenses.allMembers') }]
  if (isAliasMode.value) {
    const aliasBalances = summary.value.balances as AliasBalance[]
    return [
      { value: null, label: t('expenses.allAliases') },
      ...aliasBalances.map(b => ({
        value: b.aliasId,
        label: b.aliasName,
      })),
    ]
  }
  const normalBalances = summary.value.balances as NormalBalance[]
  return [
    { value: null, label: t('expenses.allMembers') },
    ...normalBalances.map(b => ({
      value: b.userId,
      label: `${b.user.firstName} ${b.user.lastName}`,
    })),
  ]
})

const getGroupTotal = () => {
  if (!summary.value?.balances) return 0
  const normalBalances = summary.value.balances as NormalBalance[]
  return normalBalances.reduce((total, balance) => total + Number(balance.totalPaid), 0)
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
  const empty: ExpenseFilters = { startDate: undefined, endDate: undefined, category: undefined, userId: undefined, search: undefined }
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
