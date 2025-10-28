<template>
  <div class="flex flex-col items-center justify-center p-4">
    <UCard class="w-full max-w-md">
      <template #header>
        <UiCardHeader
          :title="title"
          variant="centered"
        />
      </template>
      <UForm
        :state="form"
        class="space-y-4"
        @submit="onSubmit"
      >
        <UFormField
          label="First Name"
          name="firstName"
          required
        >
          <UInput
            v-model="form.firstName"
            placeholder="Enter first name"
            required
            size="lg"
            class="w-full"
          />
        </UFormField>
        <UFormField
          label="Last Name"
          name="lastName"
        >
          <UInput
            v-model="form.lastName"
            placeholder="Enter last name (optional)"
            size="lg"
            class="w-full"
          />
        </UFormField>
        <UFormField
          label="Email"
          name="email"
          required
        >
          <UInput
            v-model="form.email"
            type="email"
            placeholder="Enter email address"
            required
            size="lg"
            class="w-full"
          />
        </UFormField>

        <UFormField
          label="Role"
          name="globalRoleId"
          required
        >
          <USelect
            v-model="form.globalRoleId"
            :items="roleOptions"
            placeholder="Select role"
            size="lg"
            class="w-full"
          />
        </UFormField>
        <div class="flex gap-3">
          <UButton
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
    default: () => ({
      firstName: '',
      lastName: '',
      email: '',
      globalRoleId: UserRole.BASE_USER,
    }),
  },
  loading: {
    type: Boolean,
    default: false,
  },
  isEdit: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['submit', 'cancel'])

const roleOptions = UserRole.getSelectOptions()

const form = ref({ ...props.initialData })

watch(() => props.initialData, (newData) => {
  form.value = { ...newData }
}, { deep: true })

function onSubmit() {
  emit('submit', { ...form.value })
}
</script>
