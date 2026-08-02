<template>
  <div v-if="visibleItems.length > 0">
    <UDropdownMenu
      v-if="iconOnly"
      :items="visibleItems"
    >
      <UButton
        :icon="dropdownIcon"
        :color="color"
        :variant="variant"
        :size="size"
        :disabled="disabled"
      />
    </UDropdownMenu>
    <UFieldGroup v-else>
      <UButton
        :label="label"
        :color="color"
        :variant="variant"
        :size="size"
        :disabled="disabled"
      />
      <UDropdownMenu :items="visibleItems">
        <UButton
          :icon="dropdownIcon"
          :color="color"
          :variant="variant"
          :size="size"
          :disabled="disabled"
        />
      </UDropdownMenu>
    </UFieldGroup>
  </div>
</template>

<script setup lang="ts">
interface Props {
  label?: string
  items?: { label?: string, visible?: boolean, [key: string]: unknown }[]
  color?: 'primary' | 'secondary' | 'success' | 'error' | 'info' | 'warning' | 'neutral'
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl'
  variant?: 'solid' | 'outline' | 'soft' | 'subtle' | 'ghost' | 'link'
  disabled?: boolean
  dropdownIcon?: string
  iconOnly?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  label: undefined,
  items: () => [],
  color: 'primary',
  size: 'md',
  variant: 'solid',
  disabled: false,
  dropdownIcon: 'i-lucide-chevron-down',
  iconOnly: false,
})

// Filter items based on visible property (if exists)
const visibleItems = computed(() => {
  return props.items.filter((item) => {
    // If item has a 'visible' property, use it; otherwise show the item
    return item.visible !== undefined ? item.visible : true
  })
})
</script>
