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

<script setup lang="ts">
interface Props {
  icon: string
  title: string
  subtitle?: string | null
  iconClass?: string
}
withDefaults(defineProps<Props>(), {
  subtitle: null,
  iconClass: 'size-12 text-dimmed mx-auto',
})
</script>
