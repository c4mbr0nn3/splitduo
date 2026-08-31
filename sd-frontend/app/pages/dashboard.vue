<template>
  <div class="py-6 sm:py-8">
    <UiCardHeader
      size="lg"
      :title="$t('dashboard.title')"
      class="mb-6"
    />

    <!-- Quick Actions (mobile only) -->
    <UCard
      class="sd-surface h-fit lg:hidden mb-8"
      :ui="{ body: 'p-4 sm:p-5 space-y-3' }"
    >
      <p class="text-xs font-medium text-muted uppercase tracking-wide">
        {{ $t('dashboard.quickActions') }}
      </p>
      <UButton
        v-for="(action, index) in quickActions"
        :key="action.id"
        :to="action.to"
        :icon="action.icon"
        :label="action.label"
        :variant="index === 0 ? 'solid' : 'outline'"
        :color="index === 0 ? 'primary' : 'neutral'"
        size="lg"
        block
        class="justify-start"
        @click="action.onClick?.()"
      />
    </UCard>

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
      <UCard
        class="sd-surface h-fit hidden lg:block"
        :ui="{ body: 'p-4 sm:p-5 space-y-3' }"
      >
        <p class="text-xs font-medium text-muted uppercase tracking-wide">
          {{ $t('dashboard.quickActions') }}
        </p>
        <UButton
          v-for="(action, index) in quickActions"
          :key="action.id"
          :to="action.to"
          :icon="action.icon"
          :label="action.label"
          :variant="index === 0 ? 'solid' : 'outline'"
          :color="index === 0 ? 'primary' : 'neutral'"
          size="lg"
          block
          class="justify-start"
          @click="action.onClick?.()"
        />
      </UCard>
    </div>

    <DashboardSettleUpModal v-model:open="showSettleUp" />
  </div>
</template>

<script setup lang="ts">
const { t } = useI18n()

const { groups, fetchGroups } = useGroups()
const { userStats, fetchUserStats } = useUsers()

const showSkeleton = ref(true)
const showSettleUp = ref(false)

const stats = computed(() => ({
  individual: userStats.value?.individual ?? { groups: 0, youOwe: 0, youreOwed: 0 },
  alias: userStats.value?.alias ?? { groups: 0, youOwe: 0, youreOwed: 0 },
}))

onMounted(async () => {
  try {
    await withMinDuration(async () => {
      await Promise.all([fetchGroups({ limit: 3 }), fetchUserStats()])
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

const quickActions = computed<{ id: string, label: string, icon: string, to?: string, onClick?: () => void }[]>(() => [
  {
    id: 'create-group',
    label: t('dashboard.createNewGroup'),
    icon: 'i-lucide-plus',
    to: '/groups/add',
  },
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
].filter(a => a.id !== 'settle-up' || groups.value.length > 0))

useHead({
  title: computed(() => t('dashboard.title')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
