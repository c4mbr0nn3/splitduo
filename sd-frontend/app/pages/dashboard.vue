<template>
  <div class="py-6 sm:py-8">
    <UiCardHeader
      size="lg"
      :title="$t('dashboard.title')"
      class="mb-6"
    />

    <!-- System update banner (admins only, when a new version is available) -->
    <SystemUpdateBanner
      v-if="isGlobalAdmin && updateNotification"
      :notification="updateNotification"
    />

    <!-- Quick Actions (mobile only) -->
    <DashboardQuickActionsCard
      :actions="quickActions"
      class="lg:hidden mb-8"
    />

    <!-- Stats: two balance widgets — stacked on mobile, side-by-side on desktop -->
    <div class="mb-8">
      <!-- Skeleton: two widget skeletons — stacked on mobile, side-by-side on desktop -->
      <section
        v-if="showSkeleton"
        class="grid grid-cols-1 lg:grid-cols-2 gap-4"
      >
        <DashboardBalanceWidgetSkeleton
          v-for="i in 2"
          :key="i"
        />
      </section>

      <section
        v-else
        class="grid grid-cols-1 lg:grid-cols-2 gap-4 sd-stagger"
      >
        <DashboardBalanceWidget
          :label="t('dashboard.personalGroups')"
          :groups="Number(stats.individual.groups)"
          :you-owe="Number(stats.individual.youOwe)"
          :youre-owed="Number(stats.individual.youreOwed)"
        />
        <DashboardBalanceWidget
          :label="t('dashboard.sharedGroups')"
          :groups="Number(stats.alias.groups)"
          :you-owe="Number(stats.alias.youOwe)"
          :youre-owed="Number(stats.alias.youreOwed)"
        />
      </section>
    </div>
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
      <UCard class="lg:col-span-2">
        <template #header>
          <div class="flex items-center justify-between">
            <h2 class="text-lg font-semibold">
              {{ $t('dashboard.recentGroups') }}
            </h2>
            <UButton
              size="sm"
              variant="outline"
              color="neutral"
              :label="$t('dashboard.viewAll')"
              @click="viewAllGroups"
            />
          </div>
        </template>

        <div
          v-if="showSkeleton"
          class="space-y-4"
        >
          <DashboardGroupCardSkeleton
            v-for="i in 3"
            :key="i"
          />
        </div>

        <UiEmptyState
          v-else-if="groups.length === 0"
          icon="i-lucide-users"
          :title="$t('dashboard.noGroupsTitle')"
          :subtitle="$t('dashboard.noGroupsSubtitle')"
        >
          <template #action>
            <UButton
              :label="$t('dashboard.createFirstGroup')"
              @click="createFirstGroup"
            />
          </template>
        </UiEmptyState>

        <div
          v-else
          class="space-y-4"
        >
          <template
            v-for="group in groups"
            :key="group.id"
          >
            <DashboardGroupCard
              :group="group"
            />
          </template>
        </div>
      </UCard>
      <!-- Quick Actions (desktop only) -->
      <DashboardQuickActionsCard
        :actions="quickActions"
        class="hidden lg:block"
      />
    </div>

    <DashboardSettleUpModal v-model:open="showSettleUp" />
    <DashboardRecurringModal v-model:open="showRecurring" />
  </div>
</template>

<script setup lang="ts">
import type { QuickAction } from '~/components/dashboard/QuickActionsCard.vue'

const { t } = useI18n()

const { groups, fetchGroups } = useGroups()
const { userStats, fetchUserStats } = useUsers()
const { isGlobalAdmin } = useAuth()
const { notifications, fetchSystemNotifications } = useSystemNotifications()

const updateNotification = computed(() =>
  notifications.value.find(n => n.type === 'update-available'),
)

const showSkeleton = ref(true)
const showSettleUp = ref(false)
const showRecurring = ref(false)

const stats = computed(() => ({
  individual: userStats.value?.individual ?? { groups: 0, youOwe: 0, youreOwed: 0 },
  alias: userStats.value?.alias ?? { groups: 0, youOwe: 0, youreOwed: 0 },
}))

onMounted(async () => {
  try {
    await withMinDuration(async () => {
      await Promise.all([fetchGroups({ limit: 3 }), fetchUserStats()])
      await fetchSystemNotifications()
    })
  }
  catch (error: unknown) {
    console.error('Failed to fetch dashboard data:', error)
  }
  finally {
    showSkeleton.value = false
  }
})

const createFirstGroup = () => {
  navigateTo('/groups/add')
}

const viewAllGroups = () => {
  navigateTo('/groups')
}

const quickActions = computed<QuickAction[]>(() => [
  {
    id: 'add-expense',
    label: t('dashboard.addExpense'),
    icon: 'i-lucide-receipt',
    to: '/expenses/add',
  },
  {
    id: 'settle-up',
    label: t('dashboard.settleUp'),
    icon: 'i-lucide-arrow-right-left',
    onClick: () => { showSettleUp.value = true },
  },
  {
    id: 'recurring',
    label: t('dashboard.recurringExpense'),
    icon: 'i-lucide-repeat',
    onClick: () => { showRecurring.value = true },
  },
  {
    id: 'create-group',
    label: t('dashboard.createNewGroup'),
    icon: 'i-lucide-plus',
    to: '/groups/add',
  },
].filter(a => a.id === 'create-group' || groups.value.length > 0))

useHead({
  title: computed(() => t('dashboard.title')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
