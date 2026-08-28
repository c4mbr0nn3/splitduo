<template>
  <div>
    <div
      v-if="isLoadingStats"
      class="space-y-6"
    >
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <USkeleton class="h-32" />
        <USkeleton class="h-32" />
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4 lg:gap-6 items-stretch">
        <USkeleton class="lg:col-span-2 h-64" />
        <USkeleton class="h-64" />
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4 lg:gap-6 items-stretch">
        <USkeleton class="lg:col-span-2 h-64" />
        <div class="lg:col-span-1 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-1 gap-3">
          <USkeleton class="h-24" />
          <USkeleton class="h-24" />
          <USkeleton class="h-24" />
        </div>
      </div>
    </div>
    <div
      v-else-if="groupStats"
      class="space-y-6"
    >
      <GroupsStatsCards
        :expense-count="Number(groupStats.expenseCount)"
        :group-total="Number(groupStats.totalAmount)"
        :is-alias-mode="isAliasMode"
      />

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4 lg:gap-6 items-stretch">
        <GroupsStatsMonthlyChart
          v-if="groupStats.monthlyBreakdown?.length"
          class="lg:col-span-2"
          :monthly-breakdown="groupStats.monthlyBreakdown as MonthlyStat[]"
        />
        <GroupsStatsCategoryChart
          v-if="groupStats.categoryBreakdown?.length"
          :category-breakdown="groupStats.categoryBreakdown as CategoryStat[]"
        />
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4 lg:gap-6 items-stretch">
        <GroupsStatsMemberPaidChart
          v-if="groupStats.balances?.length"
          class="lg:col-span-2"
          :balances="groupStats.balances as NormalBalance[]"
        />
        <div
          v-if="groupStats.balances?.length"
          class="lg:col-span-1"
        >
          <h3 class="text-lg font-semibold text-primary mb-3">
            {{ isAliasMode ? $t('stats.aliasBalances') : $t('stats.memberBalances') }}
          </h3>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-1 gap-3">
            <template
              v-if="isAliasMode"
            >
              <GroupsAliasBalanceCard
                v-for="balance in groupStats.balances"
                :key="(balance as AliasBalance).aliasId"
                :balance="balance as AliasBalance"
              />
            </template>
            <template
              v-else
            >
              <GroupsMemberBalanceCard
                v-for="balance in groupStats.balances"
                :key="(balance as NormalBalance).userId"
                :balance="balance as NormalBalance"
              />
            </template>
          </div>
        </div>
      </div>
    </div>
    <UiEmptyState
      v-else
      icon="i-lucide-bar-chart-3"
      :title="$t('stats.noStatsAvailable')"
      :subtitle="$t('stats.noStatsSubtitle')"
    />
  </div>
</template>

<script setup lang="ts">
import type { AliasBalance, NormalBalance, CategoryStat, MonthlyStat } from '~/types/domain'

interface Props {
  groupId: string
}
const props = defineProps<Props>()

const { groupStats, fetchGroupStats, isLoading: isLoadingStats, isAliasMode } = useBalances(props.groupId)

onMounted(async () => {
  await fetchGroupStats()
})
</script>
