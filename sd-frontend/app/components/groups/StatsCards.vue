<template>
  <div class="grid grid-cols-1 gap-4">
    <UCard variant="outline">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 bg-teal-100 rounded-full flex items-center justify-center">
          <UIcon
            name="i-lucide-clipboard-list"
            class="w-5 h-5 text-teal-600"
          />
        </div>
        <div>
          <p class="text-sm text-muted">
            Total Expenses
          </p>
          <p class="font-bold text-lg text-teal-600">
            {{ totalExpenses || 0 }}
          </p>
        </div>
      </div>
    </UCard>

    <UCard variant="outline">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 bg-green-100 rounded-full flex items-center justify-center">
          <UIcon
            name="i-lucide-calculator"
            class="w-5 h-5 text-green-600"
          />
        </div>
        <div>
          <p class="text-sm text-muted">
            Group Total
          </p>
          <p class="font-bold text-lg text-green-600">
            {{ formatAmount(groupTotal) }}€
          </p>
        </div>
      </div>
    </UCard>
  </div>
</template>

<script setup>
defineProps({
  totalExpenses: {
    type: Number,
    default: 0,
  },
  groupTotal: {
    type: Number,
    default: 0,
  },
})
</script>
