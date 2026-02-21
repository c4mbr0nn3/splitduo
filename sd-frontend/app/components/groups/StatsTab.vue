<template>
  <div>
    <UiLoadingSpinner
      v-if="isLoadingStats"
      text="Loading stats..."
    />
    <div
      v-else-if="groupStats"
      class="space-y-6"
    >
      <GroupsStatsCards
        :total-expenses="groupStats.totalExpenses"
        :group-total="groupStats.totalAmount"
      />
      <GroupsStatsCategoryChart
        v-if="groupStats.categoryBreakdown?.length"
        :category-breakdown="groupStats.categoryBreakdown"
      />
      <GroupsStatsMonthlyChart
        v-if="groupStats.monthlyBreakdown?.length"
        :monthly-breakdown="groupStats.monthlyBreakdown"
      />
      <GroupsStatsMemberPaidChart
        v-if="groupStats.balances?.length"
        :balances="groupStats.balances"
      />
      <div>
        <h3 class="text-lg font-semibold text-primary mb-3">
          Member Balances
        </h3>
        <div class="space-y-3">
          <GroupsMemberBalanceCard
            v-for="balance in groupStats.balances"
            :key="balance.userId"
            :balance="balance"
          />
        </div>
      </div>
    </div>
    <UiEmptyState
      v-else
      icon="i-lucide-bar-chart-3"
      title="No stats available"
      subtitle="Add some expenses to see group stats"
    />
  </div>
</template>

<script setup>
const props = defineProps({
  groupId: { type: String, required: true },
})

const { groupStats, fetchGroupStats, isLoading: isLoadingStats } = useBalances(props.groupId)

onMounted(async () => {
  await fetchGroupStats()
})
</script>
