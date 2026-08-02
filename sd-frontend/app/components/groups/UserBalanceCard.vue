<template>
  <UCard
    v-if="balance"
    variant="outline"
    class="h-full"
  >
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-3">
        <div
          class="w-12 h-12 rounded-full flex items-center justify-center"
          :class="Number(balance.balance) >= 0 ? 'bg-success/10' : 'bg-error/10'"
        >
          <UIcon
            :name="Number(balance.balance) >= 0 ? 'i-lucide-trending-up' : 'i-lucide-trending-down'"
            :class="Number(balance.balance) >= 0 ? 'text-success' : 'text-error'"
            class="w-6 h-6"
          />
        </div>
        <div>
          <p class="text-sm text-muted">
            {{ isAliasMode ? $t('stats.yourAlias', { name: balance.aliasName || '—' }) : $t('stats.yourNetBalance') }}
          </p>
          <p
            class="font-bold text-2xl"
            :class="Number(balance.balance) >= 0 ? 'text-success' : 'text-error'"
          >
            {{ Number(balance.balance) >= 0 ? '+' : '' }}{{ formatAmount(balance.balance) }} €
          </p>
        </div>
      </div>
    </div>
    <USeparator />
    <div class="grid grid-cols-2 gap-4 pt-4">
      <div class="text-center">
        <p class="text-xs text-dimmed mb-1">
          {{ isAliasMode ? $t('stats.aliasPaid') : $t('stats.youPaid') }}
        </p>
        <p class="font-semibold text-success">
          {{ formatAmount(balance.totalPaid) }} €
        </p>
      </div>
      <div class="text-center">
        <p class="text-xs text-dimmed mb-1">
          {{ isAliasMode ? $t('stats.aliasOwes') : $t('stats.youOwe') }}
        </p>
        <p class="font-semibold text-warning">
          {{ formatAmount(balance.totalOwed) }} €
        </p>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
interface BalanceDisplay {
  balance: number
  totalPaid: number
  totalOwed: number
  aliasName?: string
}

interface Props {
  balance?: BalanceDisplay | null
  isAliasMode?: boolean
}
withDefaults(defineProps<Props>(), {
  balance: null,
  isAliasMode: false,
})
</script>
