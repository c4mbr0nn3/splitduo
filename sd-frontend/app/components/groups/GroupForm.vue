<template>
  <div class="flex flex-col items-center justify-center p-4">
    <UCard class="w-full max-w-md">
      <template #header>
        <h2 class="text-xl font-bold text-center">
          {{ title }}
        </h2>
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
        <UButton
          type="submit"
          :label="submitLabel"
          block
          size="lg"
          :loading="loading"
        />
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

const emit = defineEmits(['submit'])

const form = ref({ ...props.initialData })

watch(() => props.initialData, (newData) => {
  form.value = { ...newData }
}, { deep: true })

function onSubmit() {
  emit('submit', { ...form.value })
}
</script>
