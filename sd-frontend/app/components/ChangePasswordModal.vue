<script setup>
const { t } = useI18n()
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

  if (password.length < 8) errors.push(t('auth.atLeast8Chars'))
  if (!/[A-Z]/.test(password)) errors.push(t('auth.oneUppercase'))
  if (!/[a-z]/.test(password)) errors.push(t('auth.oneLowercase'))
  if (!/[0-9]/.test(password)) errors.push(t('auth.oneDigit'))
  if (!/[!@#$%^&*()_+\-=[\]{}|;:,.<>?]/.test(password)) errors.push(t('auth.oneSpecialChar'))

  return errors.length > 0 ? t('auth.passwordMustContain', { errors: errors.join(', ') }) : null
})

const confirmPasswordError = computed(() => {
  if (!passwordForm.value.confirmPassword) return null
  if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
    return t('auth.passwordsDoNotMatch')
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

    showSuccess(t('changePassword.successMessage'))
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
    const errorMessage = error?.data?.error?.message || error?.message || t('changePassword.failedMessage')
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
        :title="$t('changePassword.title')"
        :subtitle="$t('changePassword.subtitle')"
      />
    </template>

    <template #body>
      <UForm
        :state="passwordForm"
        class="space-y-4 w-full"
        @submit.prevent="handlePasswordChange"
      >
        <UFormField
          :label="$t('changePassword.currentPassword')"
          name="currentPassword"
          required
        >
          <UInput
            v-model="passwordForm.currentPassword"
            type="password"
            :placeholder="$t('changePassword.enterCurrentPassword')"
            autocomplete="current-password"
            required
            class="w-full"
            :disabled="isChangingPassword"
          />
        </UFormField>

        <UFormField
          :label="$t('changePassword.newPassword')"
          name="newPassword"
          required
          :error="passwordValidationError"
        >
          <UInput
            v-model="passwordForm.newPassword"
            type="password"
            :placeholder="$t('changePassword.enterNewPassword')"
            autocomplete="new-password"
            required
            class="w-full"
            :disabled="isChangingPassword"
          />
          <template #help>
            <p class="text-xs text-muted mt-1">
              {{ $t('changePassword.passwordRequirements') }}
            </p>
          </template>
        </UFormField>

        <UFormField
          :label="$t('changePassword.confirmNewPassword')"
          name="confirmPassword"
          required
          :error="confirmPasswordError"
        >
          <UInput
            v-model="passwordForm.confirmPassword"
            type="password"
            :placeholder="$t('changePassword.confirmNewPasswordPlaceholder')"
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
          {{ $t('changePassword.cancel') }}
        </UButton>
        <UButton
          color="primary"
          :loading="isChangingPassword"
          :disabled="isChangingPassword || !isPasswordFormValid"
          @click="handlePasswordChange"
        >
          {{ $t('changePassword.changePassword') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
