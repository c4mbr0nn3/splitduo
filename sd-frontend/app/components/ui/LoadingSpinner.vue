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

<script setup lang="ts">
interface Props {
  icon?: string
  text?: string | null
  containerClass?: string
  spinnerClass?: string
}
withDefaults(defineProps<Props>(), {
  icon: 'i-lucide-loader-2',
  text: null,
  containerClass: 'flex justify-center py-8',
  spinnerClass: 'w-6 h-6 animate-spin text-muted',
})
</script>
