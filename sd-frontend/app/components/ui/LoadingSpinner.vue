<template>
  <div :class="containerClass">
    <UIcon
      :name="icon"
      :class="spinnerClass"
    />
    <p
      v-if="text"
      class="text-sm text-muted mt-2"
    >
      {{ text }}
    </p>
  </div>
</template>

<script setup>
defineProps({
  icon: {
    type: String,
    default: 'i-lucide-loader-2',
  },
  text: {
    type: String,
    default: null,
  },
  containerClass: {
    type: String,
    default: 'flex justify-center py-8',
  },
  spinnerClass: {
    type: String,
    default: 'w-6 h-6 animate-spin text-muted',
  },
})
</script>
