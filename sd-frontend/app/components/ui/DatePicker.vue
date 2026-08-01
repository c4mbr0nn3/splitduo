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

const { t, locale } = useI18n()

const props = defineProps({
  placeholder: { type: String, default: null },
  size: { type: String, default: 'md' },
})

const modelValue = defineModel({ type: String, default: null })

const df = computed(() => new DateFormatter(locale.value, { dateStyle: 'medium' }))

const calendarValue = computed({
  get: () => (modelValue.value ? parseDate(modelValue.value) : undefined),
  set: (val) => { modelValue.value = val ? val.toString() : null },
})

const displayLabel = computed(() =>
  modelValue.value
    ? df.value.format(parseDate(modelValue.value).toDate(getLocalTimeZone()))
    : (props.placeholder || t('common.selectDate')),
)
</script>
