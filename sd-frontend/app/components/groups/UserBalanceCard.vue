<template>
  <UCard
    v-if="balance"
    variant="outline"
  >
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-3">
        <div
          class="w-12 h-12 rounded-full flex items-center justify-center"
          :class="balance.balance >= 0 ? 'bg-green-100' : 'bg-red-100'"
        >
          <UIcon
            :name="balance.balance >= 0 ? 'i-lucide-trending-up' : 'i-lucide-trending-down'"
            :class="balance.balance >= 0 ? 'text-green-600' : 'text-red-600'"
            class="w-6 h-6"
          />
        </div>
        <div>
          <p class="text-sm text-muted">
            Your Net Balance
          </p>
          <p
            class="font-bold text-2xl"
            :class="balance.balance >= 0 ? 'text-green-600' : 'text-red-600'"
          >
            {{ balance.balance >= 0 ? '+' : '' }}{{ balance.balance.toFixed(2) }} €
          </p>
        </div>
      </div>
    </div>
    <USeparator />
    <div class="grid grid-cols-2 gap-4 pt-4">
      <div class="text-center">
        <p class="text-xs text-dimmed mb-1">
          You Paid
        </p>
        <p class="font-semibold text-blue-600">
          {{ balance.totalPaid.toFixed(2) }} €
        </p>
      </div>
      <div class="text-center">
        <p class="text-xs text-dimmed mb-1">
          You Owe
        </p>
        <p class="font-semibold text-orange-600">
          {{ balance.totalOwed.toFixed(2) }} €
        </p>
      </div>
    </div>
  </UCard>
</template>

<script setup>
defineProps({
  balance: {
    type: Object,
    default: null,
  },
})
</script>
