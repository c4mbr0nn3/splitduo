<template>
  <UCard
    class="sd-surface sd-surface-hover cursor-pointer"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <NuxtLink
      :to="`/groups/${group.id}`"
      class="block"
    >
      <div class="flex items-start justify-between gap-4">
        <div class="flex items-center gap-3 min-w-0">
          <div class="shrink-0 w-10 h-10 rounded-full border border-neutral-200 dark:border-neutral-700 bg-neutral-100/50 dark:bg-neutral-800/50 flex items-center justify-center text-muted">
            <UIcon
              name="i-lucide-users"
              class="size-5"
            />
          </div>
          <div class="min-w-0">
            <h3 class="text-base font-semibold text-highlighted truncate">{{ group.name }}</h3>
            <p class="text-sm text-muted mt-1">{{ group.memberCount || 0 }} member{{ group.memberCount === 1 ? '' : 's' }}</p>
          </div>
        </div>
        <span @click.stop>
          <UiButtonDropdown
            icon-only
            dropdown-icon="i-lucide-ellipsis-vertical"
            size="md"
            square
            variant="ghost"
            color="neutral"
            :items="dropdownItems"
          />
        </span>
      </div>
      <div class="flex items-center justify-between mt-3">
        <p class="text-xs text-dimmed">
          Updated {{ formatDate(group.updatedAt) }}
        </p>
        <UBadge
          v-if="net !== 0"
          :color="badgeColor"
          variant="subtle"
          class="sd-tabular whitespace-nowrap"
        >
          {{ badgeLabel }}
        </UBadge>
        <UBadge
          v-else
          color="neutral"
          variant="subtle"
          class="whitespace-nowrap"
        >
          settled
        </UBadge>
      </div>
    </NuxtLink>
  </UCard>
</template>

<script setup>
const props = defineProps({
  group: {
    type: Object,
    required: true,
  },
})

const net = computed(() => props.group.netBalance ?? 0)
const badgeColor = computed(() => net.value > 0 ? 'success' : net.value < 0 ? 'error' : 'neutral')
const badgeLabel = computed(() => {
  if (net.value > 0) return `owed €${formatAmount(net.value)}`
  if (net.value < 0) return `owes €${formatAmount(Math.abs(net.value))}`
  return 'settled'
})

const dropdownItems = computed(() => [
  {
    label: 'View Group',
    icon: 'i-lucide-eye',
    color: 'info',
    onSelect: () => navigateTo(`/groups/${props.group.id}`),
  },
])
</script>
