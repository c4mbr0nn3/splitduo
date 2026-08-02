<template>
  <div class="flex flex-col items-center justify-center py-6 sm:py-8 px-4">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <UiCardHeader :title="title" />
      </template>
      <UForm
        :state="form"
        class="space-y-4"
        @submit="onSubmit"
      >
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <UFormField
            :label="$t('admin.firstName')"
            name="firstName"
            required
          >
            <UInput
              v-model="form.firstName"
              :placeholder="$t('admin.enterFirstName')"
              required
              size="lg"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('admin.lastName')"
            name="lastName"
          >
            <UInput
              v-model="form.lastName"
              :placeholder="$t('admin.enterLastName')"
              size="lg"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('admin.email')"
            name="email"
            required
          >
            <UInput
              v-model="form.email"
              type="email"
              :placeholder="$t('admin.enterEmail')"
              required
              size="lg"
              class="w-full"
            />
          </UFormField>

          <UFormField
            :label="$t('admin.role')"
            name="globalRoleId"
            required
          >
            <USelect
              v-model="form.globalRoleId"
              :items="roleOptions"
              :placeholder="$t('admin.selectRole')"
              size="lg"
              class="w-full"
            />
          </UFormField>
        </div>
        <div class="flex flex-col-reverse sm:flex-row sm:justify-end gap-3 mt-6">
          <UButton
            :label="$t('admin.back')"
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

<script setup lang="ts">
interface UserFormData {
  firstName: string
  lastName: string
  email: string
  globalRoleId: number
}

interface Props {
  title: string
  submitLabel: string
  initialData?: UserFormData
  loading?: boolean
  isEdit?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  initialData: () => ({
    firstName: '',
    lastName: '',
    email: '',
    globalRoleId: UserRole.BASE_USER,
  }),
  loading: false,
  isEdit: false,
})

const emit = defineEmits<{
  submit: [data: UserFormData]
  cancel: []
}>()

const roleOptions = UserRole.getSelectOptions()

const form = ref({ ...props.initialData })

watch(() => props.initialData, (newData) => {
  form.value = { ...newData }
}, { deep: true })

function onSubmit() {
  emit('submit', { ...form.value })
}
</script>
