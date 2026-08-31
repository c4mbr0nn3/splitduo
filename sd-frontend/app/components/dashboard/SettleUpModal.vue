<script setup lang="ts">
import type { Group } from '~/types/domain'
import { buildSettleUpUrl } from '~/utils/settle'
import { formatCurrency } from '~/utils/currency'

const isOpen = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const { user } = useAuth()
const { groups, fetchGroups } = useGroups()
const { fetchAliases } = useAliases()

// Set before fetching on selection; useBalances guards fetches on it.
const selectedGroupId = ref('')
const { balanceSummary, isAliasMode, fetchGroup, fetchBalanceSummary } = useBalances(selectedGroupId)

const isLoadingGroups = ref(false)
const isSelecting = ref(false)
const loadError = ref(false)

// Fetch the full group list each time the picker opens (dashboard only
// preloads 3; the picker must list all of the user's groups).
watch(isOpen, async (open) => {
  if (!open) return
  loadError.value = false
  isLoadingGroups.value = true
  try {
    await fetchGroups({ limit: 100 })
  }
  catch {
    loadError.value = true // toast already shown by useGroups
  }
  finally {
    isLoadingGroups.value = false
  }
})

const balanceColor = (group: Group) => {
  const netBalance = Number(group.netBalance)
  if (netBalance > 0) return 'success'
  if (netBalance < 0) return 'error'
  return 'neutral'
}

const balanceLabel = (group: Group) => {
  const balance = Number(group.netBalance)
  if (balance > 0) return t('groups.owed', { amount: formatCurrency(balance) })
  if (balance < 0) return t('groups.owes', { amount: formatCurrency(Math.abs(balance)) })
  return t('groups.settled')
}

const onSelectGroup = async (group: Group) => {
  if (isSelecting.value) return
  isSelecting.value = true
  loadError.value = false
  try {
    const groupId = group.id
    selectedGroupId.value = groupId
    await fetchGroup(groupId) // required: populates currentGroup → isAliasMode
    let currentAliasId: string | null = null
    if (isAliasMode.value) {
      const aliases = await fetchAliases(groupId) ?? []
      currentAliasId = aliases.find(a => a.members?.some(m => m.id === user.value?.id))?.id ?? null
    }
    await fetchBalanceSummary()
    const url = buildSettleUpUrl({
      groupId,
      suggestions: (balanceSummary.value?.suggestions ?? []) as never,
      isAliasMode: isAliasMode.value,
      currentUserId: user.value?.id ?? '',
      currentAliasId,
    })
    isOpen.value = false
    await navigateTo(url)
  }
  catch {
    loadError.value = true // toasts already shown by composables
  }
  finally {
    isSelecting.value = false
  }
}
</script>

<template>
  <UModal
    v-model:open="isOpen"
    :dismissible="!isSelecting"
    :title="t('dashboard.settleUp')"
    :description="t('dashboard.settleUpPickGroup')"
  >
    <template #body>
      <UiLoadingSpinner
        v-if="isLoadingGroups"
        :text="t('settle.loading')"
      />
      <UiEmptyState
        v-else-if="loadError"
        icon="i-lucide-circle-alert"
        :title="t('dashboard.settleUpFailed')"
      />
      <UiEmptyState
        v-else-if="groups.length === 0"
        icon="i-lucide-users"
        :title="t('dashboard.noGroupsTitle')"
        :subtitle="t('dashboard.noGroupsSubtitle')"
      />
      <div
        v-else
        class="space-y-2"
      >
        <UButton
          v-for="group in groups"
          :key="group.id"
          variant="outline"
          color="neutral"
          size="lg"
          block
          class="justify-start"
          :loading="isSelecting"
          :disabled="isSelecting"
          icon="i-lucide-users"
          @click="onSelectGroup(group)"
        >
          <span class="flex-1 min-w-0 truncate text-left">{{ group.name }}</span>
          <UBadge
            :color="balanceColor(group)"
            variant="subtle"
            size="sm"
            class="shrink-0 font-semibold sd-tabular"
            :label="balanceLabel(group)"
          />
        </UButton>
      </div>
    </template>
  </UModal>
</template>
