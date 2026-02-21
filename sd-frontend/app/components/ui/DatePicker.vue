<template>
  <UPopover :content="{ side: 'bottom', align: 'start' }">
    <UButton
      color="neutral"
      variant="outline"
      :size="size"
      icon="i-lucide-calendar"
      class="w-full justify-start font-normal"
      :label="displayLabel"
    />
    <template #content>
      <UCalendar
        v-model="calendarValue"
        class="p-2"
      />
    </template>
  </UPopover>
</template>

<script setup>
import { parseDate, getLocalTimeZone, DateFormatter } from '@internationalized/date'

const props = defineProps({
  placeholder: { type: String, default: 'Select date' },
  size: { type: String, default: 'sm' },
})

const modelValue = defineModel({ type: String, default: null })

const df = new DateFormatter('en-GB', { dateStyle: 'medium' })

const calendarValue = computed({
  get: () => (modelValue.value ? parseDate(modelValue.value) : undefined),
  set: (val) => { modelValue.value = val ? val.toString() : null },
})

const displayLabel = computed(() =>
  modelValue.value
    ? df.format(parseDate(modelValue.value).toDate(getLocalTimeZone()))
    : props.placeholder,
)
</script>
