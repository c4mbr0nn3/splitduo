<template>
  <UFieldGroup v-if="visibleItems.length > 0">
    <UButton
      :label="label"
      :variant="variant"
      :size="size"
      :disabled="disabled"
    />
    <UDropdownMenu :items="visibleItems">
      <UButton
        :icon="dropdownIcon"
        :variant="variant"
        :size="size"
        :disabled="disabled"
      />
    </UDropdownMenu>
  </UFieldGroup>
</template>

<script setup>
const props = defineProps({
  label: {
    type: String,
    required: true,
  },
  items: {
    type: Array,
    default: () => [],
  },
  color: {
    type: String,
    default: 'primary',
  },
  size: {
    type: String,
    default: 'md',
  },
  variant: {
    type: String,
    default: 'solid',
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  dropdownIcon: {
    type: String,
    default: 'i-heroicons-chevron-down',
  },
})

// Filter items based on visible property (if exists)
const visibleItems = computed(() => {
  return props.items.filter((item) => {
    // If item has a 'visible' property, use it; otherwise show the item
    return item.visible !== undefined ? item.visible : true
  })
})
</script>
