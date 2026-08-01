<template>
  <div>
    <UiLoadingSpinner
      v-if="isLoadingStats"
      :text="$t('stats.loading')"
    />
    <div
      v-else-if="groupStats"
      class="space-y-6"
    >
      <GroupsStatsCards
        :total-expenses="groupStats.totalExpenses"
        :group-total="groupStats.totalAmount"
        :is-alias-mode="isAliasMode"
      />

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4 lg:gap-6 items-stretch">
        <GroupsStatsMonthlyChart
          v-if="groupStats.monthlyBreakdown?.length"
          class="lg:col-span-2"
          :monthly-breakdown="groupStats.monthlyBreakdown"
        />
        <GroupsStatsCategoryChart
          v-if="groupStats.categoryBreakdown?.length"
          :category-breakdown="groupStats.categoryBreakdown"
        />
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4 lg:gap-6 items-stretch">
        <GroupsStatsMemberPaidChart
          v-if="groupStats.balances?.length"
          class="lg:col-span-2"
          :balances="groupStats.balances"
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
                :key="balance.aliasId"
                :balance="balance"
              />
            </template>
            <template
              v-else
            >
              <GroupsMemberBalanceCard
                v-for="balance in groupStats.balances"
                :key="balance.userId"
                :balance="balance"
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

<script setup>
const props = defineProps({
  groupId: { type: String, required: true },
})

const { groupStats, fetchGroupStats, isLoading: isLoadingStats, isAliasMode } = useBalances(props.groupId)

onMounted(async () => {
  await fetchGroupStats()
})
</script>
