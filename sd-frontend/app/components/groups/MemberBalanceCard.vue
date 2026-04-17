<template>
  <UCard variant="outline">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <UAvatar
          :alt="`${balance.user.firstName} ${balance.user.lastName || ''}`.trim()"
          icon="i-lucide-user"
          size="md"
        />
        <p class="font-semibold text-primary">
          {{ balance.user.firstName }} {{ balance.user.lastName }}
        </p>
      </div>
      <span
        class="font-bold text-lg"
        :class="balance.balance >= 0 ? 'text-green-600' : 'text-red-600'"
      >
        {{ balance.balance >= 0 ? '+' : '' }}{{ formatAmount(balance.balance) }} €
      </span>
    </div>
    <USeparator class="my-3" />
    <div class="grid grid-cols-2 gap-4 text-center">
      <div>
        <p class="text-xs text-dimmed mb-1">
          Paid
        </p>
        <p class="font-semibold text-teal-600">
          {{ formatAmount(balance.totalPaid) }} €
        </p>
      </div>
      <div>
        <p class="text-xs text-dimmed mb-1">
          Owes
        </p>
        <p class="font-semibold text-orange-600">
          {{ formatAmount(balance.totalOwed) }} €
        </p>
      </div>
    </div>
  </UCard>
</template>

<script setup>
defineProps({
  balance: {
    type: Object,
    required: true,
  },
})
</script>
