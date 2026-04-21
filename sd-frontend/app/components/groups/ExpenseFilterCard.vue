<template>
  <UCard variant="outline">
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <span class="text-primary font-semibold">Filters</span>
        <div class="flex items-center gap-1">
          <UButton
            icon="i-lucide-funnel-x"
            label="Clear"
            size="xs"
            variant="ghost"
            :color="activeFilterCount === 0 ? 'gray' : 'error'"
            :disabled="activeFilterCount === 0"
            @click="$emit('clear')"
          />
          <UButton
            label="Apply"
            size="xs"
            :disabled="pendingFilterCount === 0"
            @click="$emit('apply')"
          />
        </div>
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">Search</label>
        <UInput
          v-model="filters.search"
          placeholder="Search title or description..."
          class="w-full"
        />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">From</label>
        <UiInputDate v-model="filters.startDate" />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">To</label>
        <UiInputDate v-model="filters.endDate" />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">Category</label>
        <USelect
          v-model="filters.category"
          :items="categoryOptions"
          class="w-full"
        />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">Paid by</label>
        <USelect
          v-model="filters.userId"
          :items="memberOptions"
          class="w-full"
        />
      </div>
    </div>
  </UCard>
</template>

<script setup>
const filters = defineModel('filters', { type: Object, required: true })

defineProps({
  categoryOptions: { type: Array, required: true },
  memberOptions: { type: Array, required: true },
  activeFilterCount: { type: Number, default: 0 },
})

defineEmits(['apply', 'clear'])

const pendingFilterCount = computed(() =>
  Object.values(filters.value).filter(Boolean).length,
)
</script>
