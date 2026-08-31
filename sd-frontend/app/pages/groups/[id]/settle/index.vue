<template>
  <div class="py-6 sm:py-8">
    <UiLoadingSpinner
      v-if="isLoading"
      :text="$t('settle.loading')"
    />

    <UiEmptyState
      v-else-if="loadError"
      icon="i-lucide-users"
      :title="$t('groups.unableToLoad')"
    >
      <template #action>
        <UButton
          color="primary"
          variant="outline"
          size="sm"
          @click="retryLoad"
        >
          {{ $t('groups.retry') }}
        </UButton>
      </template>
    </UiEmptyState>

    <template v-else>
      <UiCardHeader
        :title="group?.name || $t('settle.title')"
        :subtitle="summaryLine"
        :back-to="`/groups/${groupId}`"
        class="mb-6"
      >
        <template #actions>
          <UButton
            icon="i-lucide-arrow-right-left"
            size="sm"
            :to="`/groups/${groupId}/settle/confirm`"
          >
            <span class="hidden sm:inline">{{ $t('settle.recordSettlement') }}</span>
          </UButton>
        </template>
      </UiCardHeader>

      <UCard
        class="sd-surface"
        :ui="{ body: 'p-4 sm:p-6' }"
      >
        <div class="space-y-6">
          <div v-if="activeSuggestions.length">
            <GroupsSettleList
              :group-id="groupId"
              :suggestions="activeSuggestions"
              :current-user-id="currentUserId"
              :is-alias-mode="isAliasMode"
              :current-alias-id="currentAliasId"
              :user-map="userMap"
            />
          </div>

          <UiEmptyState
            v-else
            icon="i-lucide-check-circle-2"
            :title="$t('settle.allSettled')"
            :subtitle="$t('settle.allSettledSubtitle')"
            icon-class="size-16 text-success mx-auto"
          >
            <template #action>
              <UButton
                :to="`/groups/${groupId}/settle/confirm`"
                color="primary"
                variant="outline"
                size="sm"
                icon="i-lucide-arrow-right-left"
                class="mt-4"
              >
                {{ $t('settle.recordSettlement') }}
              </UButton>
            </template>
          </UiEmptyState>
        </div>
      </UCard>

      <UCard
        class="sd-surface mt-6"
        :ui="{ body: 'p-4 sm:p-6' }"
      >
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-primary">
            {{ $t('settle.historyTitle') }}
          </h2>
        </div>
        <div
          v-if="settlements.length"
          class="space-y-2"
        >
          <div
            v-for="s in settlements"
            :key="s.id"
            class="flex items-center gap-3 py-2 border-b border-[var(--sd-surface-border)] last:border-0"
          >
            <UIcon
              name="i-lucide-arrow-right-left"
              class="w-5 h-5 text-info shrink-0"
            />
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="font-medium text-highlighted truncate">{{ settlementFromLabel(s) }}</span>
                <UIcon
                  name="i-lucide-arrow-right"
                  class="w-4 h-4 text-dimmed shrink-0"
                />
                <span class="font-medium text-highlighted truncate">{{ settlementToLabel(s) }}</span>
              </div>
              <p class="text-xs text-dimmed truncate">
                {{ formatDateString(s.date) }}<template v-if="s.description">
                  · {{ s.description }}
                </template>
              </p>
            </div>
            <span class="font-semibold text-highlighted sd-tabular shrink-0">{{ formatCurrency(s.amount) }}</span>
            <UButton
              icon="i-lucide-trash-2"
              color="error"
              variant="ghost"
              size="sm"
              :disabled="isDeletingSettlement"
              @click="removeSettlement(s)"
            />
          </div>
        </div>
        <p
          v-else
          class="text-sm text-muted"
        >
          {{ $t('settle.historyEmpty') }}
        </p>
      </UCard>
    </template>
  </div>
</template>

<script setup lang="ts">
import type { BalanceSuggestion, AliasSettlementSuggestion, NormalBalance, Settlement } from '~/types/domain'
import { formatCurrency } from '~/utils/currency'

definePageMeta({
  middleware: 'auth',
})

const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)
const { user } = useAuth()
const { balanceSummary, fetchBalanceSummary, isAliasMode, group, fetchGroup } = useBalances(groupId)
const { aliases, fetchAliases } = useAliases()
const { settlements, fetchSettlements, deleteSettlement } = useSettlements(groupId)
const modal = useModal()

const isLoading = ref(true)
const loadError = ref(false)
const isDeletingSettlement = ref(false)

const currentUserId = computed(() => user.value?.id || '')

const currentAliasId = computed(() => {
  if (!isAliasMode.value || !user.value?.id) return null
  const userAlias = aliases.value.find(a => a.members?.some(m => m.id === user.value!.id))
  return userAlias?.id || null
})

const userMap = computed<Record<string, { firstName: string, lastName?: string | null }>>(() => {
  if (isAliasMode.value) return {}
  const summary = balanceSummary.value
  if (!summary?.balances) return {}
  const normalBalances = summary.balances as NormalBalance[]
  return Object.fromEntries(normalBalances.map(b => [b.userId, { firstName: b.user?.firstName ?? '', lastName: b.user?.lastName ?? null }]))
})

const activeSuggestions = computed(() => {
  const summary = balanceSummary.value
  if (!summary?.suggestions) return [] as BalanceSuggestion[] | AliasSettlementSuggestion[]
  return summary.suggestions as BalanceSuggestion[] | AliasSettlementSuggestion[]
})

const totalSettlementAmount = computed(() =>
  activeSuggestions.value.reduce((total, s) => total + (Number(s.amount) || 0), 0),
)

const summaryLine = computed(() => {
  const count = activeSuggestions.value.length
  return t('settle.summaryLine', {
    count,
    amount: formatCurrency(totalSettlementAmount.value),
  })
})

const settlementFromLabel = (s: Settlement) =>
  s.fromUser?.firstName || s.paidByAliasName || t('settle.someone')

const settlementToLabel = (s: Settlement) =>
  s.toUser?.firstName || s.toAliasName || t('settle.someone')

const removeSettlement = async (s: Settlement) => {
  const confirmed = await modal.error({
    title: t('settle.deleteTitle'),
    subtitle: t('settle.deleteConfirm'),
    confirmText: t('settle.deleteButton'),
    cancelText: t('common.cancel'),
  })

  if (!confirmed) return

  isDeletingSettlement.value = true
  try {
    await deleteSettlement(s.id)
    await fetchBalanceSummary()
    await fetchSettlements({ limit: 10 })
  }
  finally {
    isDeletingSettlement.value = false
  }
}

const retryLoad = async () => {
  loadError.value = false
  await loadData()
}

const loadData = async () => {
  isLoading.value = true
  loadError.value = false
  try {
    await fetchGroup(groupId)
    if (isAliasMode.value) {
      await fetchAliases(groupId)
    }

    await fetchBalanceSummary()
    await fetchSettlements({ limit: 10 })
  }
  catch {
    loadError.value = true
  }
  finally {
    isLoading.value = false
  }
}

onMounted(async () => {
  await loadData()
})

useHead({
  title: computed(() => group.value?.name
    ? t('settle.titleForGroup', { name: group.value.name })
    : t('settle.title')),
})
</script>
