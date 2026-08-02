<template>
  <UCard
    class="bg-[var(--sd-panel-bg)] border-[var(--sd-surface-border)]"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <span class="text-primary font-semibold">{{ $t('expenses.filterTitle') }}</span>
        <div class="flex items-center gap-1">
          <UButton
            icon="i-lucide-funnel-x"
            :label="$t('expenses.filterClear')"
            size="xs"
            variant="ghost"
            :color="activeFilterCount === 0 ? 'neutral' : 'error'"
            :disabled="activeFilterCount === 0"
            @click="$emit('clear')"
          />
          <UButton
            :label="$t('expenses.filterApply')"
            size="xs"
            :disabled="pendingFilterCount === 0"
            @click="$emit('apply')"
          />
        </div>
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">{{ $t('expenses.filterSearch') }}</label>
        <UInput
          v-model="filters.search"
          :placeholder="$t('expenses.filterSearchPlaceholder')"
          class="w-full"
        />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">{{ $t('expenses.filterFrom') }}</label>
        <UiInputDate v-model="filters.startDate" />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">{{ $t('expenses.filterTo') }}</label>
        <UiInputDate v-model="filters.endDate" />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">{{ $t('expenses.filterCategory') }}</label>
        <USelect
          v-model="filters.category"
          :items="categoryOptions"
          class="w-full"
        />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1">{{ $t('expenses.filterPaidBy') }}</label>
        <USelect
          v-model="filters.userId"
          :items="memberOptions"
          class="w-full"
        />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
interface ExpenseFilters {
  search?: string
  startDate?: string
  endDate?: string
  category?: string
  userId?: string
}

interface SelectOption {
  value: string | null
  label: string
}

const filters = defineModel<ExpenseFilters>('filters', { required: true })

interface Props {
  categoryOptions: SelectOption[]
  memberOptions: SelectOption[]
  activeFilterCount?: number
}
defineProps<Props>()

defineEmits<{
  apply: []
  clear: []
}>()

const pendingFilterCount = computed(() =>
  Object.values(filters.value).filter(Boolean).length,
)
</script>
