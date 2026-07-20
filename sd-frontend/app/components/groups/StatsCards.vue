<template>
  <UCard variant="soft">
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
              Total Expenses
            </p>
            <p class="font-bold text-xl text-primary sd-tabular">
              {{ totalExpenses || 0 }}
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
              Group Total
            </p>
            <p class="font-bold text-xl text-success sd-tabular">
              {{ formatAmount(groupTotal) }} €
            </p>
          </div>
        </div>
      </div>

      <div
        v-if="suggestion"
        class="flex items-center gap-3"
      >
        <div
          class="w-10 h-10 rounded-full flex items-center justify-center"
          :class="suggestion.isOwed ? 'bg-success/10' : 'bg-warning/10'"
        >
          <UIcon
            name="i-lucide-hand-coins"
            class="w-5 h-5"
            :class="suggestion.isOwed ? 'text-success' : 'text-warning'"
          />
        </div>
        <div>
          <p class="text-sm text-muted">
            Settle up
          </p>
          <p
            class="font-bold text-xl sd-tabular"
            :class="suggestion.isOwed ? 'text-success' : 'text-warning'"
          >
            {{ suggestion.label }}
          </p>
        </div>
      </div>
    </div>
  </UCard>
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
  suggestion: {
    type: Object,
    default: null,
  },
})
</script>
