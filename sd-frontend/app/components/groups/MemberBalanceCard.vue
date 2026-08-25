<template>
  <UCard variant="outline">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <UserAvatar
          :user="balance.user as UserBasicInfo"
          size="md"
        />
        <p class="font-semibold text-primary">
          {{ balance.user.firstName }} {{ balance.user.lastName }}
        </p>
      </div>
      <span
        class="font-bold text-lg"
        :class="Number(balance.balance) >= 0 ? 'text-success' : 'text-error'"
      >
        {{ Number(balance.balance) >= 0 ? '+' : '' }}{{ formatCurrency(balance.balance) }}
      </span>
    </div>
    <div class="grid grid-cols-2 gap-4 text-center mt-3">
      <div>
        <p class="text-xs text-dimmed mb-1">
          {{ $t('stats.paid') }}
        </p>
        <p class="font-semibold text-success sd-tabular">
          {{ formatCurrency(balance.totalPaid) }}
        </p>
      </div>
      <div>
        <p class="text-xs text-dimmed mb-1">
          {{ $t('stats.owes') }}
        </p>
        <p class="font-semibold text-warning sd-tabular">
          {{ formatCurrency(balance.totalOwed) }}
        </p>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { NormalBalance, UserBasicInfo } from '~/types/domain'
import { formatCurrency } from '~/utils/currency'

interface Props {
  balance: NormalBalance
}
defineProps<Props>()
</script>
