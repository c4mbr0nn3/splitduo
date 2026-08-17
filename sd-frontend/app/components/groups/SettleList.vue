<template>
  <div class="space-y-6">
    <!-- Your settlements -->
    <div v-if="yourSuggestions.length">
      <h2 class="text-base font-semibold text-highlighted mb-3">
        {{ $t('settle.yourSettlements') }}
      </h2>
      <div class="space-y-2">
        <UCard
          v-for="item in yourSuggestions"
          :key="item.key"
          variant="soft"
          :ui="{ body: 'p-3' }"
        >
          <div class="flex items-center gap-3">
            <div
              class="w-10 h-10 rounded-full flex items-center justify-center shrink-0"
              :class="item.isOutgoing ? 'bg-warning/10' : 'bg-success/10'"
            >
              <UIcon
                :name="item.isOutgoing ? 'i-lucide-trending-down' : 'i-lucide-trending-up'"
                class="w-5 h-5"
                :class="item.isOutgoing ? 'text-warning' : 'text-success'"
              />
            </div>
            <div class="flex-1 min-w-0">
              <p class="text-sm text-muted">
                {{ item.isOutgoing ? $t('settle.youOweLabel') : $t('settle.owedToYou') }}
              </p>
              <div class="flex items-center gap-2 flex-wrap">
                <span class="font-semibold text-highlighted truncate">{{ item.fromLabel }}</span>
                <UIcon
                  name="i-lucide-arrow-right"
                  class="w-4 h-4 text-dimmed shrink-0"
                />
                <span class="font-semibold text-highlighted truncate">{{ item.toLabel }}</span>
              </div>
            </div>
            <p
              class="font-bold text-lg sd-tabular shrink-0"
              :class="item.isOutgoing ? 'text-warning' : 'text-success'"
            >
              {{ formatAmount(item.amount) }} €
            </p>
          </div>
        </UCard>
      </div>
    </div>

    <!-- Other settlements -->
    <div v-if="otherSuggestions.length">
      <h2 class="text-base font-semibold text-highlighted mb-3">
        {{ $t('settle.otherSettlements') }}
      </h2>
      <div class="space-y-2">
        <UCard
          v-for="item in otherSuggestions"
          :key="item.key"
          variant="soft"
          :ui="{ body: 'p-3' }"
        >
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-full bg-elevated flex items-center justify-center shrink-0">
              <UIcon
                name="i-lucide-arrow-right-left"
                class="w-5 h-5 text-dimmed"
              />
            </div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="font-semibold text-highlighted truncate">{{ item.fromLabel }}</span>
                <UIcon
                  name="i-lucide-arrow-right"
                  class="w-4 h-4 text-dimmed shrink-0"
                />
                <span class="font-semibold text-highlighted truncate">{{ item.toLabel }}</span>
              </div>
            </div>
            <p class="font-bold text-lg text-highlighted sd-tabular shrink-0">
              {{ formatAmount(item.amount) }} €
            </p>
          </div>
        </UCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { BalanceSuggestion, AliasSettlementSuggestion } from '~/types/domain'

interface Props {
  suggestions: BalanceSuggestion[] | AliasSettlementSuggestion[]
  currentUserId: string
  isAliasMode: boolean
  currentAliasId?: string | null
  userMap?: Record<string, { firstName: string, lastName?: string | null }>
}

const props = withDefaults(defineProps<Props>(), {
  currentAliasId: null,
  userMap: () => ({}),
})

const { t } = useI18n()

interface NormalizedSuggestion {
  key: string
  fromLabel: string
  toLabel: string
  amount: number
  isOutgoing: boolean
  isIncoming: boolean
}

const isAliasSuggestion = (s: BalanceSuggestion | AliasSettlementSuggestion): s is AliasSettlementSuggestion => {
  return props.isAliasMode && 'fromAliasId' in s
}

const isCurrentUserDebtor = (s: BalanceSuggestion | AliasSettlementSuggestion): boolean => {
  if (isAliasSuggestion(s)) {
    return s.fromAliasId === props.currentAliasId
  }
  return s.fromUserId === props.currentUserId
}

const isCurrentUserCreditor = (s: BalanceSuggestion | AliasSettlementSuggestion): boolean => {
  if (isAliasSuggestion(s)) {
    return s.toAliasId === props.currentAliasId
  }
  return s.toUserId === props.currentUserId
}

const normalize = (s: BalanceSuggestion | AliasSettlementSuggestion, index: number): NormalizedSuggestion => {
  if (isAliasSuggestion(s)) {
    return {
      key: `${s.fromAliasId}-${s.toAliasId}-${index}`,
      fromLabel: s.fromAliasName,
      toLabel: s.toAliasName,
      amount: Number(s.amount) || 0,
      isOutgoing: isCurrentUserDebtor(s),
      isIncoming: isCurrentUserCreditor(s),
    }
  }

  const normal = s as BalanceSuggestion
  return {
    key: `${normal.fromUserId}-${normal.toUserId}-${index}`,
    fromLabel: props.userMap[normal.fromUserId]?.firstName || t('settle.someone'),
    toLabel: props.userMap[normal.toUserId]?.firstName || t('settle.someone'),
    amount: Number(normal.amount) || 0,
    isOutgoing: isCurrentUserDebtor(normal),
    isIncoming: isCurrentUserCreditor(normal),
  }
}

const normalized = computed(() => props.suggestions.map((s, index) => normalize(s, index)))

const yourSuggestions = computed(() =>
  normalized.value
    .filter(item => item.isOutgoing || item.isIncoming)
    .sort((a, b) => Number(b.isOutgoing) - Number(a.isOutgoing)),
)

const otherSuggestions = computed(() =>
  normalized.value.filter(item => !item.isOutgoing && !item.isIncoming),
)

const formatAmount = (amount: number) => {
  return new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount)
}
</script>
