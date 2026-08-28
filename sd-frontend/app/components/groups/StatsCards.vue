<template>
  <UCard
    variant="soft"
    class="h-full"
  >
    <div class="space-y-4">
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center">
            <UIcon
              name="i-lucide-clipboard-list"
              class="w-5 h-5 text-primary"
            />
          </div>
          <div>
            <p class="text-sm text-muted">
              {{ $t('stats.expenseCount') }}
            </p>
            <p class="font-bold text-xl text-primary sd-tabular">
              {{ expenseCount }}
            </p>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="w-10 h-10 bg-success/10 rounded-full flex items-center justify-center">
            <UIcon
              name="i-lucide-calculator"
              class="w-5 h-5 text-success"
            />
          </div>
          <div>
            <p class="text-sm text-muted">
              {{ $t('stats.groupTotal') }}
            </p>
            <p class="font-bold text-xl text-success sd-tabular">
              {{ formatCurrency(groupTotal) }}
            </p>
          </div>
        </div>
      </div>

      <div
        v-if="groupId"
        class="flex items-center gap-3 pt-4 border-t border-default"
      >
        <div class="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center">
          <UIcon
            name="i-lucide-hand-coins"
            class="w-5 h-5 text-primary"
          />
        </div>
        <div class="flex-1">
          <p class="text-sm text-muted">
            {{ $t('stats.settleUp') }}
          </p>
          <UButton
            :to="`/groups/${groupId}/settle`"
            variant="link"
            color="primary"
            class="p-0 h-auto font-bold text-lg"
            trailing-icon="i-lucide-arrow-right"
          >
            {{ $t('settle.viewAll') }}
          </UButton>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { formatCurrency } from '~/utils/currency'

// expenseCount = number of expense records (plain integer, not currency)
// groupTotal = summed currency amount of all expenses
interface Props {
  expenseCount?: number
  groupTotal?: number
  groupId?: string | null
  isAliasMode?: boolean
}
withDefaults(defineProps<Props>(), {
  expenseCount: 0,
  groupTotal: 0,
  groupId: null,
  isAliasMode: false,
})
</script>
