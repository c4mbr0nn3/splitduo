<template>
  <div class="flex flex-col items-center justify-center py-6 sm:py-8">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <UiCardHeader :title="title" />
      </template>
      <UForm
        :state="form"
        class="space-y-4"
        @submit="onSubmit"
      >
        <div class="grid grid-cols-1 gap-4">
          <UFormField
            label="Group Name"
            name="name"
            required
          >
            <UInput
              v-model="form.name"
              placeholder="Enter group name"
              required
              size="lg"
              class="w-full"
            />
          </UFormField>
          <UFormField
            label="Description"
            name="description"
          >
            <UInput
              v-model="form.description"
              placeholder="Enter description (optional)"
              size="lg"
              class="w-full"
            />
          </UFormField>
          <UFormField
            label="Use member aliases (subgroups, e.g., couples)"
            name="useAliases"
            description="Members are grouped into sub-units; expenses split by subgroup instead of by person. Cannot be changed after creation."
          >
            <USwitch
              v-model="form.useAliases"
              :disabled="disabledAliases"
            />
          </UFormField>
          <p
            v-if="disabledAliases"
            class="text-xs text-muted -mt-2"
          >
            Set at creation; cannot be changed.
          </p>
        </div>
        <div class="flex flex-col-reverse sm:flex-row sm:justify-end gap-3 mt-6">
          <UButton
            label="Back"
            variant="ghost"
            color="neutral"
            @click="emit('cancel')"
          />
          <UButton
            type="submit"
            :label="submitLabel"
            :loading="loading"
          />
        </div>
      </UForm>
    </UCard>
  </div>
</template>

<script setup>
const props = defineProps({
  title: {
    type: String,
    required: true,
  },
  submitLabel: {
    type: String,
    required: true,
  },
  initialData: {
    type: Object,
    default: () => ({ name: '', description: '', useAliases: false }),
  },
  loading: {
    type: Boolean,
    default: false,
  },
  disabledAliases: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['submit', 'cancel'])

const form = ref({
  name: '',
  description: '',
  useAliases: false,
  ...props.initialData,
})

watch(() => props.initialData, (newData) => {
  form.value = {
    name: '',
    description: '',
    useAliases: false,
    ...newData,
  }
}, { deep: true })

function onSubmit() {
  emit('submit', { ...form.value })
}
</script>
