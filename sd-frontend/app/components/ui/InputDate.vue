<template>
  <UInputDate
    ref="inputDate"
    v-model="calendarValue"
    :size="size"
    class="w-full"
  >
    <template #trailing>
      <UPopover :reference="inputDate?.inputsRef?.[3]?.$el">
        <UButton
          icon="i-lucide-calendar"
          color="neutral"
          variant="ghost"
          size="xs"
        />
        <template #content>
          <UCalendar
            v-model="calendarValue"
            class="p-2"
          />
        </template>
      </UPopover>
    </template>
  </UInputDate>
</template>

<script setup lang="ts">
import { parseDate } from '@internationalized/date'

interface Props {
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl'
}
withDefaults(defineProps<Props>(), {
  size: 'md',
})

const modelValue = defineModel<string | null>({ default: null })

const inputDate = useTemplateRef('inputDate')

const calendarValue = computed({
  get: () => (modelValue.value ? parseDate(modelValue.value) : undefined),
  set: (val) => { modelValue.value = val ? val.toString() : null },
})
</script>
