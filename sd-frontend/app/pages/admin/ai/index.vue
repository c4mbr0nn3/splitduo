<template>
  <div class="py-6 sm:py-8">
    <UiCardHeader
      size="lg"
      :title="$t('admin.ai.title')"
      :subtitle="$t('admin.ai.subtitle')"
      class="mb-6"
    >
      <template #actions>
        <UButton
          icon="i-lucide-refresh-cw"
          variant="ghost"
          size="sm"
          square
          :loading="isLoadingList || isLoadingSummary"
          :aria-label="$t('common.refresh')"
          @click="refreshAll"
        />
      </template>
    </UiCardHeader>

    <!-- Config header -->
    <UCard
      class="sd-surface mb-4 sm:mb-6"
      :ui="{ body: 'p-4 sm:p-5' }"
    >
      <div class="flex flex-wrap items-center gap-x-6 gap-y-3">
        <div class="flex items-center gap-2">
          <span class="text-sm text-muted">{{ $t('admin.ai.configStatus') }}</span>
          <UBadge
            :color="aiEnabled ? 'success' : 'neutral'"
            variant="subtle"
            icon="i-lucide-sparkles"
          >
            {{ aiEnabled ? $t('admin.ai.statusEnabled') : $t('admin.ai.statusDisabled') }}
          </UBadge>
        </div>
        <div
          v-if="configModel"
          class="flex items-center gap-2 min-w-0"
        >
          <span class="text-sm text-muted">{{ $t('admin.ai.configModel') }}</span>
          <span class="text-sm font-medium text-highlighted truncate">{{ configModel }}</span>
        </div>
        <div
          v-if="configHost"
          class="flex items-center gap-2 min-w-0"
        >
          <span class="text-sm text-muted">{{ $t('admin.ai.configHost') }}</span>
          <span class="text-sm font-medium text-highlighted truncate">{{ configHost }}</span>
        </div>
      </div>
    </UCard>

    <!-- Disabled banner: never hide the page — the ledger still works -->
    <div
      v-if="configLoaded && !aiEnabled"
      class="flex items-start gap-3 rounded-lg border border-warning/30 bg-warning/10 px-4 py-3 mb-4 sm:mb-6"
    >
      <UIcon
        name="i-lucide-triangle-alert"
        class="size-5 text-warning shrink-0 mt-0.5"
      />
      <div>
        <p class="text-sm font-semibold text-highlighted">
          {{ $t('admin.ai.disabledTitle') }}
        </p>
        <p class="text-sm text-muted mt-0.5">
          {{ $t('admin.ai.disabledSubtitle') }}
        </p>
      </div>
    </div>

    <!-- Summary tiles -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 mb-4 sm:mb-6">
      <template v-if="isLoadingSummary">
        <DashboardStatCardSkeleton
          v-for="i in 4"
          :key="i"
        />
      </template>
      <template v-else>
        <DashboardStatCard
          :stats="totalCallsStats"
          icon="i-lucide-activity"
          color="teal"
        />
        <DashboardStatCard
          :stats="successRateStats"
          icon="i-lucide-circle-check"
          color="green"
        />
        <DashboardStatCard
          :stats="totalTokensStats"
          icon="i-lucide-coins"
          color="rose"
        />
        <DashboardStatCard
          :stats="avgLatencyStats"
          icon="i-lucide-gauge"
          color="info"
        />
      </template>
    </div>

    <!-- Filter bar -->
    <UCard
      class="sd-surface mb-4 sm:mb-6"
      :ui="{ body: 'p-4 sm:p-5' }"
    >
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <div>
          <label class="block text-sm font-medium mb-1">{{ $t('admin.ai.filterDateRange') }}</label>
          <USelect
            v-model="preset"
            :items="presetOptions"
            class="w-full"
          />
        </div>
        <div>
          <label class="block text-sm font-medium mb-1">{{ $t('admin.ai.filterStatus') }}</label>
          <USelect
            v-model="statusFilter"
            :items="statusOptions"
            class="w-full"
          />
        </div>
        <div>
          <label class="block text-sm font-medium mb-1">{{ $t('admin.ai.filterUser') }}</label>
          <USelect
            v-model="selectedUser"
            :items="userOptions"
            class="w-full"
          />
        </div>
        <div
          v-if="preset === 'custom'"
          class="grid grid-cols-2 gap-3 sm:col-span-2 lg:col-span-1"
        >
          <div>
            <label class="block text-sm font-medium mb-1">{{ $t('admin.ai.filterFrom') }}</label>
            <UiInputDate
              v-model="customFrom"
              size="md"
            />
          </div>
          <div>
            <label class="block text-sm font-medium mb-1">{{ $t('admin.ai.filterTo') }}</label>
            <UiInputDate
              v-model="customTo"
              size="md"
            />
          </div>
        </div>
      </div>
    </UCard>

    <!-- Ledger table -->
    <UTable
      :data="tableData"
      :columns="columns"
      :loading="isLoadingList"
      :sticky="'header'"
    >
      <template #empty>
        <UiEmptyState
          icon="i-lucide-sparkles"
          :title="$t('admin.ai.emptyTitle')"
          :subtitle="emptySubtitle"
        />
      </template>
    </UTable>

    <!-- Pagination -->
    <template v-if="totalPages > 1">
      <USeparator class="mt-4" />
      <div class="flex justify-center mt-4">
        <UPagination
          v-model:page="currentPage"
          :items-per-page="itemsPerPage"
          :total="Number(pagination.total)"
          :sibling-count="1"
        />
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { AiUsageEntry } from '~/types/domain'

const { t } = useI18n()

definePageMeta({
  middleware: ['auth', 'admin'],
  layout: 'default',
})

const {
  entries,
  summary,
  config,
  pagination,
  isLoadingList,
  isLoadingSummary,
  fetchUsage,
  fetchSummary,
  fetchConfig,
} = useAdminAiUsage()

const { users, fetchUsers } = useUsers()

// --- Config ---
const aiEnabled = computed(() => config.value?.enabled ?? false)
const configModel = computed(() => config.value?.model ?? null)
const configHost = computed(() => config.value?.baseUrlHost ?? null)
const configLoaded = computed(() => config.value !== null)

// --- Filters ---
type Preset = 'last7' | 'last30' | 'last90' | 'all' | 'custom'

const preset = ref<Preset>('last30')
const statusFilter = ref<'all' | 'success' | 'failure'>('all')
const customFrom = ref<string | null>(null)
const customTo = ref<string | null>(null)

const presetOptions = computed(() => [
  { value: 'last7', label: t('admin.ai.presetLast7') },
  { value: 'last30', label: t('admin.ai.presetLast30') },
  { value: 'last90', label: t('admin.ai.presetLast90') },
  { value: 'all', label: t('admin.ai.presetAllTime') },
  { value: 'custom', label: t('admin.ai.presetCustom') },
] as Array<{ value: Preset, label: string }>)

const statusOptions = computed(() => [
  { value: 'all', label: t('admin.ai.statusAll') },
  { value: 'success', label: t('admin.ai.statusSuccess') },
  { value: 'failure', label: t('admin.ai.statusFailure') },
] as Array<{ value: 'all' | 'success' | 'failure', label: string }>)

const selectedUser = ref('all')

const userOptions = computed(() => [
  { value: 'all', label: t('admin.ai.filterUserAll') },
  ...users.value.map(u => ({
    value: u.id,
    label: `${u.firstName} ${u.lastName ?? ''}`.trim() || u.email,
  })),
])

/** Convert a `yyyy-MM-dd` string to Unix seconds (end of day for `to`). */
const toUnixSeconds = (dateStr: string, endOfDay = false): number => {
  const date = new Date(`${dateStr}T00:00:00`)
  if (endOfDay) date.setHours(23, 59, 59)
  return Math.floor(date.getTime() / 1000)
}

const presetDays: Record<Exclude<Preset, 'all' | 'custom'>, number> = {
  last7: 7,
  last30: 30,
  last90: 90,
}

const dateRange = computed(() => {
  if (preset.value === 'all') return {}
  if (preset.value === 'custom') {
    const range: { from?: number, to?: number } = {}
    if (customFrom.value) range.from = toUnixSeconds(customFrom.value)
    if (customTo.value) range.to = toUnixSeconds(customTo.value, true)
    return range
  }
  const nowSeconds = Math.floor(Date.now() / 1000)
  return {
    from: nowSeconds - presetDays[preset.value] * 86400,
    to: nowSeconds,
  }
})

// --- Pagination ---
const currentPage = ref(1)
const itemsPerPage = computed(() => Number(pagination.value.limit) || 20)
const totalPages = computed(() => Number(pagination.value.totalPages))

// --- Data loading ---
const fetchList = async () => {
  await fetchUsage({
    page: currentPage.value,
    from: dateRange.value.from,
    to: dateRange.value.to,
    ...(statusFilter.value === 'all' ? {} : { success: statusFilter.value === 'success' }),
    ...(selectedUser.value === 'all' ? {} : { userId: selectedUser.value }),
  })
}

const fetchAll = async () => {
  await Promise.all([
    fetchList(),
    fetchSummary({
      from: dateRange.value.from,
      to: dateRange.value.to,
      ...(selectedUser.value === 'all' ? {} : { userId: selectedUser.value }),
    }),
  ])
}

const refreshAll = async () => {
  await fetchAll()
}

// Changing the page refetches only the ledger list.
watch(currentPage, async () => {
  await fetchList()
})

// Changing filters resets to page 1 and refetches summary + list.
watch([preset, statusFilter, selectedUser, customFrom, customTo], async () => {
  currentPage.value = 1
  await fetchAll()
})

onMounted(async () => {
  await Promise.all([
    fetchAll(),
    fetchConfig(),
  ])
  // Non-blocking: a failed users fetch must not break the page.
  fetchUsers().catch(() => {})
})

// --- Summary tiles ---
const totalCallsStats = computed(() => ({
  label: t('admin.ai.statTotalCalls'),
  value: Number(summary.value?.totalCalls ?? 0),
  color: 'teal',
}))

const successRateStats = computed(() => ({
  label: t('admin.ai.statSuccessRate'),
  value: summary.value
    ? Number(summary.value.totalCalls) > 0 && Number(summary.value.failedCalls) > 0
      ? `${(Number(summary.value.successRate) * 100).toFixed(1)}%`
      : `${(Number(summary.value.successRate) * 100).toFixed(0)}%`
    : '—',
  color: 'green',
}))

const totalTokensStats = computed(() => ({
  label: t('admin.ai.statTotalTokens'),
  value: Number(summary.value?.totalTokens ?? 0).toLocaleString(),
  color: 'rose',
}))

const avgLatencyStats = computed(() => ({
  label: t('admin.ai.statAvgLatency'),
  value: summary.value?.avgLatencyMs != null
    ? formatDuration(Number(summary.value.avgLatencyMs))
    : '—',
  color: 'info',
}))

// Shallow copy: the composable exposes a readonly array, UTable wants a mutable one.
const tableData = computed(() => [...entries.value])

// --- Table columns ---
const formatEntryTime = (entry: AiUsageEntry): string => {
  const date = new Date(Number(entry.requestedAt) * 1000)
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const columns = computed(() => [
  {
    accessorKey: 'requestedAt',
    header: t('admin.ai.colTime'),
    cell: ({ row }) => formatEntryTime(row.original),
  },
  {
    accessorKey: 'userDisplayName',
    header: t('admin.ai.colUser'),
    cell: ({ row }) => row.original.userDisplayName || row.original.userEmail || row.original.userId,
  },
  {
    accessorKey: 'feature',
    header: t('admin.ai.colFeature'),
  },
  {
    accessorKey: 'model',
    header: t('admin.ai.colModel'),
  },
  {
    accessorKey: 'success',
    header: t('admin.ai.colStatus'),
    cell: ({ row }) => h(
      'span',
      {
        class: ['inline-flex items-center gap-1.5 text-sm font-medium', row.original.success ? 'text-success' : 'text-error'],
      },
      [
        h('span', { class: ['size-2 rounded-full shrink-0', row.original.success ? 'bg-success' : 'bg-error'] }),
        row.original.success ? t('admin.ai.statusSuccess') : t('admin.ai.statusFailure'),
      ],
    ),
  },
  {
    accessorKey: 'latencyMs',
    header: t('admin.ai.colLatency'),
    cell: ({ row }) => row.original.latencyMs != null
      ? formatDuration(Number(row.original.latencyMs))
      : '—',
  },
  {
    accessorKey: 'totalTokens',
    header: t('admin.ai.colTokens'),
    cell: ({ row }) => row.original.totalTokens != null
      ? Number(row.original.totalTokens).toLocaleString()
      : '—',
  },
] as TableColumn<AiUsageEntry>[])

// --- Empty state ---
const emptySubtitle = computed(() => {
  if (configLoaded.value && !aiEnabled.value) return t('admin.ai.emptyDisabledSubtitle')
  return t('admin.ai.emptyNoRowsSubtitle')
})

useHead({
  title: computed(() => t('admin.ai.title')),
})
</script>
