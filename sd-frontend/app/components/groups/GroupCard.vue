<template>
  <UCard
    class="sd-surface sd-surface-hover"
    :ui="{ body: 'p-3 sm:p-4' }"
  >
    <NuxtLink
      :to="`/groups/${group.id}`"
      class="block"
    >
      <!-- Top row: title + actions -->
      <div class="flex items-start justify-between gap-2 sm:gap-3">
        <div class="min-w-0">
          <h3 class="font-semibold text-highlighted text-base sm:text-lg truncate">
            {{ group.name }}
          </h3>
          <p
            v-if="group.description"
            class="text-xs sm:text-sm text-muted truncate mt-0.5"
          >
            {{ group.description }}
          </p>
        </div>

        <span @click.stop>
          <UiButtonDropdown
            icon-only
            dropdown-icon="i-lucide-ellipsis-vertical"
            size="sm"
            square
            variant="ghost"
            color="neutral"
            :items="dropdownItems"
            :disabled="isDeleting"
          />
        </span>
      </div>

      <!-- Status badges -->
      <div class="flex flex-wrap items-center gap-2 mt-3">
        <UBadge
          v-if="group.memberCount"
          variant="soft"
          color="neutral"
          icon="i-lucide-users"
          size="sm"
          :label="`${group.memberCount} member${group.memberCount === 1 ? '' : 's'}`"
        />

        <UBadge
          v-if="group.useAliases && group.aliasSetupFinalized"
          variant="soft"
          color="info"
          icon="i-lucide-layers"
          size="sm"
          label="Alias"
        />

        <UBadge
          v-if="group.useAliases && !group.aliasSetupFinalized"
          variant="soft"
          color="warning"
          icon="i-lucide-alert-triangle"
          size="sm"
          label="Alias setup pending"
        />
      </div>

      <!-- Bottom row: balance + updated -->
      <div class="flex items-end justify-between gap-2 mt-3 sm:mt-4">
        <div class="text-xs text-dimmed">
          Updated {{ formatDate(group.updatedAt) }}
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
import { formatDate } from '~/utils/date'
import { formatCurrency } from '~/utils/currency'

const props = defineProps({
  group: {
    type: Object,
    required: true,
  },
  isDeleting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['delete'])

const navigateToEdit = () => {
  navigateTo(`/groups/${props.group.id}/edit/`)
}

const handleDelete = () => {
  emit('delete', props.group)
}

const dropdownItems = computed(() => [
  {
    label: 'Edit',
    icon: 'i-lucide-edit-2',
    color: 'info',
    onSelect: navigateToEdit,
  },
  {
    type: 'separator',
  },
  {
    label: 'Delete',
    icon: 'i-lucide-trash-2',
    color: 'error',
    onSelect: handleDelete,
  },
])

const balanceColor = computed(() => {
  if (props.group.netBalance > 0) return 'success'
  if (props.group.netBalance < 0) return 'error'
  return 'neutral'
})

const balanceLabel = computed(() => {
  const balance = props.group.netBalance
  if (balance > 0) return `owed ${formatCurrency(balance)}`
  if (balance < 0) return `owes ${formatCurrency(Math.abs(balance))}`
  return 'settled'
})
</script>
