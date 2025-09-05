<template>
  <div class="text-center py-8">
    <UIcon
      :name="icon"
      :class="iconClass"
    />
    <p class="text-dimmed mb-4">
      {{ title }}
    </p>
    <p
      v-if="subtitle"
      class="text-sm text-gray-400 mt-1 mb-4"
    >
      {{ subtitle }}
    </p>
    <slot name="action" />
  </div>
</template>

<script setup>
defineProps({
  icon: {
    type: String,
    required: true,
  },
  title: {
    type: String,
    required: true,
  },
  subtitle: {
    type: String,
    default: null,
  },
  iconClass: {
    type: String,
    default: 'w-12 h-12 text-gray-300 mx-auto mb-4',
  },
})
</script>
