<template>
  <UCard
    class="sd-surface sd-surface-hover"
    :ui="{ body: 'p-3 sm:p-4' }"
  >
    <NuxtLink
      :to="`/groups/${group.id}`"
      class="block"
    >
      <!-- Title row -->
      <div class="flex items-start justify-between gap-2 sm:gap-3">
        <div class="min-w-0">
          <h3 class="font-semibold text-highlighted text-base sm:text-lg truncate">
            {{ group.name }}
          </h3>
        </div>
      </div>

      <!-- Status badges -->
      <div class="flex flex-wrap items-center gap-2 mt-3">
        <UBadge
          v-if="group.memberCount"
          variant="soft"
          color="neutral"
          icon="i-lucide-users"
          size="sm"
          :label="$t('groups.memberCount', { count: group.memberCount }, group.memberCount)"
        />

        <UBadge
          v-if="group.useAliases && group.aliasSetupFinalized"
          variant="soft"
          color="info"
          icon="i-lucide-layers"
          size="sm"
          :label="$t('groups.alias')"
        />

        <UBadge
          v-if="group.useAliases && !group.aliasSetupFinalized"
          variant="soft"
          color="warning"
          icon="i-lucide-alert-triangle"
          size="sm"
          :label="$t('groups.aliasPending')"
        />
      </div>

      <!-- Balance + updated row -->
      <div class="flex items-end justify-between gap-2 mt-3 sm:mt-4">
        <div class="text-xs text-dimmed">
          {{ $t('common.updated') }} {{ formatDate(group.updatedAt) }}
        </div>

        <UBadge
          :color="balanceColor"
          variant="subtle"
          size="sm"
          class="font-semibold sd-tabular"
          :label="balanceLabel"
        />
      </div>
    </NuxtLink>
  </UCard>
</template>

<script setup>
import { formatCurrency } from '~/utils/currency'

const { t } = useI18n()

const props = defineProps({
  group: {
    type: Object,
    required: true,
  },
})

const balanceColor = computed(() => {
  if (props.group.netBalance > 0) return 'success'
  if (props.group.netBalance < 0) return 'error'
  return 'neutral'
})

const balanceLabel = computed(() => {
  const balance = props.group.netBalance
  if (balance > 0) return t('groups.owed', { amount: formatCurrency(balance) })
  if (balance < 0) return t('groups.owes', { amount: formatCurrency(Math.abs(balance)) })
  return t('groups.settled')
})
</script>
