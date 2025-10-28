<template>
  <div class="flex flex-col items-center justify-center py-4">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <UiCardHeader :title="title" />
      </template>
      <UForm
        :state="form"
        class="space-y-4"
        @submit="onSubmit"
      >
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
        <div class="flex space-x-3 pt-4">
          <UButton
            type="button"
            label="Back"
            variant="outline"
            size="lg"
            class="flex-1"
            @click="emit('cancel')"
          />
          <UButton
            type="submit"
            :label="submitLabel"
            size="lg"
            class="flex-1"
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
    default: () => ({ name: '', description: '' }),
  },
  loading: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['submit', 'cancel'])

const form = ref({ ...props.initialData })

watch(() => props.initialData, (newData) => {
  form.value = { ...newData }
}, { deep: true })

function onSubmit() {
  emit('submit', { ...form.value })
}
</script>
