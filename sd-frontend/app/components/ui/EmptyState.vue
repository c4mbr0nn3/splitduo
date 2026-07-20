<template>
  <div class="text-center py-10 sd-fade-in">
    <UIcon
      :name="icon"
      :class="iconClass"
    />
    <h3 class="mt-4 text-base font-semibold text-highlighted">
      {{ title }}
    </h3>
    <p
      v-if="subtitle"
      class="mt-1 text-sm text-muted"
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
    default: 'size-12 text-dimmed mx-auto',
  },
})
</script>
