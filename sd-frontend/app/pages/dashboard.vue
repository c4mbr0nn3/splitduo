<template>
  <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Stats Cards -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
      <DashboardStatCard
        :stats="{ label: 'Total Groups', value: userStats?.totalGroups }"
        icon="i-lucide-users"
        color="blue"
      />
      <DashboardStatCard
        :stats="{ label: 'You Owe', value: userStats?.youOwe, color: 'red' }"
        type="currency"
        icon="i-lucide-frown"
        color="red"
      />
      <DashboardStatCard
        :stats="{ label: `You're Owed`, value: userStats?.youreOwed, color: 'green' }"
        type="currency"
        icon="i-lucide-smile"
        color="green"
      />
    </div>
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <h2 class="text-lg font-semibold">
              Recent Groups
            </h2>
            <UButton
              size="sm"
              variant="subtle"
              label="View All"
              @click="viewAllGroups"
            />
          </div>
        </template>
        <UiLoadingSpinner
          v-if="isLoadingGroups"
          text="Loading groups..."
        />

        <UiEmptyState
          v-else-if="groups.length === 0"
          icon="i-lucide-users"
          title="No groups yet"
          subtitle="Get started by creating your first group"
        >
          <template #action>
            <UButton
              label="Create Your First Group"
              @click="createFirstGroup"
            />
          </template>
        </UiEmptyState>

        <div
          v-else
          class="space-y-4"
        >
          <template
            v-for="group in groups.slice(0, 3)"
            :key="group.id"
          >
            <DashboardGroupCard
              :group="group"
              @click="navigateToGroup(group.id)"
            />
          </template>
        </div>
      </UCard>
      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">
            Quick Actions
          </h2>
        </template>

        <div class="space-y-4">
          <UButton
            v-for="action in quickActions"
            :key="action.id"
            block
            size="lg"
            :variant="action.variant"
            class="justify-start"
            :label="action.label"
            :icon="action.icon"
            @click="action.handler"
          />
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup>
const { groups, fetchGroups, isLoading: isLoadingGroups } = useGroups()
const { userStats, fetchUserStats } = useUsers()
const { showInfo } = useNotifications()

onMounted(async () => {
  try {
    await fetchGroups()
    await fetchUserStats()
  }
  catch (error) {
    console.error('Failed to fetch groups:', error)
  }
})

const navigateToGroup = (groupId) => {
  navigateTo(`/groups/${groupId}`)
}

const createFirstGroup = () => {
  navigateTo('/groups/add')
}

const viewAllGroups = () => {
  navigateTo('/groups')
}

const quickActions = [
  {
    id: 'create-group',
    label: 'Create New Group',
    icon: 'i-lucide-plus',
    variant: 'solid',
    handler: () => navigateTo('/groups/add'),
  },
  {
    id: 'add-expense',
    label: 'Add Expense',
    icon: 'i-lucide-receipt',
    variant: 'outline',
    handler: () => showInfo('Add expense coming soon!'),
  },
  {
    id: 'settle-up',
    label: 'Settle Up',
    icon: 'i-lucide-credit-card',
    variant: 'outline',
    handler: () => showInfo('Settle up coming soon!'),
  },
]

useHead({
  title: 'Dashboard',
})

definePageMeta({
  middleware: 'auth',
})
</script>
