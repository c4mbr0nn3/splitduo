<script setup>
const props = defineProps({
  open: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['update:open', 'success'])

const isOpen = computed({
  get: () => props.open,
  set: value => emit('update:open', value),
})

const { showSuccess, showError } = useNotifications()
const api = useApi()

const isChangingPassword = ref(false)

const passwordForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const passwordValidationError = computed(() => {
  if (!passwordForm.value.newPassword) return null

  const password = passwordForm.value.newPassword
  const errors = []

  if (password.length < 8) errors.push('at least 8 characters')
  if (!/[A-Z]/.test(password)) errors.push('one uppercase letter')
  if (!/[a-z]/.test(password)) errors.push('one lowercase letter')
  if (!/[0-9]/.test(password)) errors.push('one digit')
  if (!/[!@#$%^&*()_+\-=[\]{}|;:,.<>?]/.test(password)) errors.push('one special character')

  return errors.length > 0 ? `Password must contain ${errors.join(', ')}` : null
})

const confirmPasswordError = computed(() => {
  if (!passwordForm.value.confirmPassword) return null
  if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
    return 'Passwords do not match'
  }
  return null
})

const isPasswordFormValid = computed(() => {
  return (
    passwordForm.value.currentPassword
    && passwordForm.value.newPassword
    && passwordForm.value.confirmPassword
    && !passwordValidationError.value
    && !confirmPasswordError.value
  )
})

const resetForm = () => {
  passwordForm.value = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  }
}

const closePasswordModal = () => {
  if (!isChangingPassword.value) {
    resetForm()
    isOpen.value = false
  }
}

const handlePasswordChange = async () => {
  if (!isPasswordFormValid.value || isChangingPassword.value) return

  isChangingPassword.value = true

  try {
    await api.put('/users/me/password', {
      currentPassword: passwordForm.value.currentPassword,
      newPassword: passwordForm.value.newPassword,
      confirmPassword: passwordForm.value.confirmPassword,
    })

    showSuccess('Password changed successfully. All sessions have been logged out for security.')
    resetForm()
    isOpen.value = false
    emit('success')

    // Redirect to login after a brief delay (login is at home page)
    setTimeout(() => {
      const { logout } = useAuth()
      logout()
      navigateTo('/')
    }, 1500)
  }
  catch (error) {
    const errorMessage = error?.data?.error?.message || error?.message || 'Failed to change password'
    showError(errorMessage)
  }
  finally {
    isChangingPassword.value = false
  }
}
</script>

<template>
  <UModal
    v-model:open="isOpen"
    :dismissible="!isChangingPassword"
    @update:open="(value) => { if (!value) closePasswordModal() }"
  >
    <template #header>
      <UiCardHeader
        title="Change Password"
        subtitle="Update your account password"
      />
    </template>

    <template #body>
      <UForm
        :state="passwordForm"
        class="space-y-4 w-full"
        @submit.prevent="handlePasswordChange"
      >
        <UFormField
          label="Current Password"
          name="currentPassword"
          required
        >
          <UInput
            v-model="passwordForm.currentPassword"
            type="password"
            placeholder="Enter current password"
            autocomplete="current-password"
            required
            class="w-full"
            :disabled="isChangingPassword"
          />
        </UFormField>

        <UFormField
          label="New Password"
          name="newPassword"
          required
          :error="passwordValidationError"
        >
          <UInput
            v-model="passwordForm.newPassword"
            type="password"
            placeholder="Enter new password"
            autocomplete="new-password"
            required
            class="w-full"
            :disabled="isChangingPassword"
          />
          <template #help>
            <p class="text-xs text-muted mt-1">
              Must be at least 8 characters with uppercase, lowercase, digit, and special character
            </p>
          </template>
        </UFormField>

        <UFormField
          label="Confirm New Password"
          name="confirmPassword"
          required
          :error="confirmPasswordError"
        >
          <UInput
            v-model="passwordForm.confirmPassword"
            type="password"
            placeholder="Confirm new password"
            autocomplete="new-password"
            required
            class="w-full"
            :disabled="isChangingPassword"
          />
        </UFormField>
      </UForm>
    </template>

    <template #footer>
      <div class="flex gap-2 w-full">
        <UButton
          color="neutral"
          variant="outline"
          :disabled="isChangingPassword"
          class="ml-auto"
          @click="closePasswordModal"
        >
          Cancel
        </UButton>
        <UButton
          color="primary"
          :loading="isChangingPassword"
          :disabled="isChangingPassword || !isPasswordFormValid"
          @click="handlePasswordChange"
        >
          Change Password
        </UButton>
      </div>
    </template>
  </UModal>
</template>
