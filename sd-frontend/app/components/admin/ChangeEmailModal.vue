<script setup lang="ts">
const props = defineProps<{
  open?: boolean
  userId: string
  currentEmail: string
  pendingEmail?: string | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'success': []
}>()

const isOpen = computed({
  get: () => props.open,
  set: value => emit('update:open', value),
})

const { requestEmailChange } = useUsers()

const isSubmitting = ref(false)

const emailForm = ref({
  newEmail: '',
  currentPassword: '',
})

// No dedicated locale key for a "must differ" message, so the rule is
// enforced as a validity gate (submit stays disabled) instead of an
// inline error string
const isSameEmail = computed(() => {
  return emailForm.value.newEmail.trim().toLowerCase() === props.currentEmail.trim().toLowerCase()
})

const isEmailFormValid = computed(() => {
  return Boolean(
    emailForm.value.newEmail.trim()
    && emailForm.value.currentPassword
    && !isSameEmail.value,
  )
})

const resetForm = () => {
  emailForm.value = {
    newEmail: '',
    currentPassword: '',
  }
}

const closeEmailModal = () => {
  if (!isSubmitting.value) {
    resetForm()
    isOpen.value = false
  }
}

const handleEmailChange = async () => {
  if (!isEmailFormValid.value || isSubmitting.value) return

  isSubmitting.value = true

  try {
    await requestEmailChange(props.userId, {
      newEmail: emailForm.value.newEmail.trim(),
      currentPassword: emailForm.value.currentPassword,
    })

    // The composable already toasted the success message
    resetForm()
    isOpen.value = false
    emit('success')
  }
  catch {
    // The composable already toasted the error; keep the modal open
    // with the entered values so the admin can retry
  }
  finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <UModal
    v-model:open="isOpen"
    :dismissible="!isSubmitting"
    @update:open="(value) => { if (!value) closeEmailModal() }"
  >
    <template #header>
      <UiCardHeader
        :title="$t('admin.changeEmailTitle')"
        :subtitle="$t('admin.changeEmailDescription')"
      />
    </template>

    <template #body>
      <div class="space-y-4 w-full">
        <UFormField
          :label="$t('admin.email')"
          name="currentEmail"
        >
          <UInput
            :model-value="currentEmail"
            type="email"
            disabled
            class="w-full"
          />
        </UFormField>
        <UBadge
          v-if="pendingEmail"
          color="warning"
          variant="subtle"
        >
          {{ $t('admin.pendingEmail', { email: pendingEmail }) }}
        </UBadge>

        <UForm
          :state="emailForm"
          class="space-y-4 w-full"
          @submit.prevent="handleEmailChange"
        >
          <UFormField
            :label="$t('admin.newEmail')"
            name="newEmail"
            required
          >
            <UInput
              v-model="emailForm.newEmail"
              type="email"
              :placeholder="$t('admin.enterEmail')"
              autocomplete="email"
              required
              class="w-full"
              :disabled="isSubmitting"
            />
          </UFormField>

          <UFormField
            :label="$t('admin.currentPassword')"
            name="currentPassword"
            required
          >
            <UInput
              v-model="emailForm.currentPassword"
              type="password"
              autocomplete="current-password"
              required
              class="w-full"
              :disabled="isSubmitting"
            />
          </UFormField>
        </UForm>
      </div>
    </template>

    <template #footer>
      <div class="flex gap-2 w-full">
        <UButton
          color="neutral"
          variant="outline"
          :disabled="isSubmitting"
          class="ml-auto"
          @click="closeEmailModal"
        >
          {{ $t('admin.cancel') }}
        </UButton>
        <UButton
          color="primary"
          :loading="isSubmitting"
          :disabled="isSubmitting || !isEmailFormValid"
          @click="handleEmailChange"
        >
          {{ $t('admin.sendVerification') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
