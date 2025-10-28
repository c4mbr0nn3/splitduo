<script setup>
const route = useRoute()
const api = useApi()
const { showSuccess, showError } = useNotifications()

const email = ref(route.query.email || '')
const token = ref(route.query.token || '')

const isValidating = ref(true)
const isTokenValid = ref(false)
const isResetting = ref(false)
const resetSuccess = ref(false)

const passwordForm = ref({
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
    passwordForm.value.newPassword
    && passwordForm.value.confirmPassword
    && !passwordValidationError.value
    && !confirmPasswordError.value
  )
})

const validateToken = async () => {
  if (!email.value || !token.value) {
    isValidating.value = false
    isTokenValid.value = false
    showError('Invalid or missing reset token')
    return
  }

  try {
    await api.get('/auth/validate-reset-token', {
      email: email.value,
      token: token.value,
    })

    isTokenValid.value = true
  }
  catch (error) {
    isTokenValid.value = false
    const errorMessage = error?.data?.error?.message || 'Invalid or expired reset token'
    showError(errorMessage)
  }
  finally {
    isValidating.value = false
  }
}

const handlePasswordReset = async () => {
  if (!isPasswordFormValid.value || isResetting.value) return

  isResetting.value = true

  try {
    await api.post('/auth/reset-password', {
      email: email.value,
      token: token.value,
      newPassword: passwordForm.value.newPassword,
      confirmPassword: passwordForm.value.confirmPassword,
    })

    resetSuccess.value = true
    showSuccess('Password reset successful! Redirecting to login...')

    // Redirect to login after 2 seconds
    setTimeout(() => {
      navigateTo('/')
    }, 2000)
  }
  catch (error) {
    const errorMessage = error?.data?.error?.message || 'Failed to reset password'
    showError(errorMessage)
  }
  finally {
    isResetting.value = false
  }
}

onMounted(() => {
  validateToken()
})

useHead({
  title: 'Reset Password',
})

definePageMeta({
  layout: 'auth',
})
</script>

<template>
  <div class="flex min-h-[80vh] items-center justify-center">
    <UCard class="w-full max-w-md">
      <template #header>
        <UiCardHeader
          title="Reset Your Password"
          subtitle="Create a new password for your account"
        />
      </template>

      <!-- Validating Token -->
      <div
        v-if="isValidating"
        class="flex flex-col items-center justify-center py-8 space-y-4"
      >
        <USkeleton class="h-8 w-8 rounded-full" />
        <p class="text-sm text-muted">
          Validating reset token...
        </p>
      </div>

      <!-- Invalid Token -->
      <div
        v-else-if="!isTokenValid"
        class="space-y-4"
      >
        <div class="flex justify-center">
          <UIcon
            name="i-lucide-x-circle"
            class="size-16 text-error"
          />
        </div>

        <div class="text-center space-y-2">
          <h4 class="font-semibold">
            Invalid or Expired Link
          </h4>
          <p class="text-sm text-muted">
            This password reset link is invalid or has expired.
          </p>
          <p class="text-sm text-muted">
            Reset links expire after 1 hour for security.
          </p>
        </div>

        <div class="flex flex-col gap-2 pt-4">
          <NuxtLink to="/forgot-password">
            <UButton
              size="lg"
              class="w-full"
            >
              Request New Link
            </UButton>
          </NuxtLink>

          <NuxtLink
            to="/"
            class="text-center text-sm text-muted hover:text-primary transition-colors"
          >
            Back to Login
          </NuxtLink>
        </div>
      </div>

      <!-- Reset Success -->
      <div
        v-else-if="resetSuccess"
        class="space-y-4"
      >
        <div class="flex justify-center">
          <UIcon
            name="i-lucide-check-circle-2"
            class="size-16 text-success"
          />
        </div>

        <div class="text-center space-y-2">
          <h4 class="font-semibold">
            Password Reset Successful!
          </h4>
          <p class="text-sm text-muted">
            Your password has been updated successfully.
          </p>
          <p class="text-sm text-muted">
            Redirecting to login...
          </p>
        </div>
      </div>

      <!-- Password Reset Form -->
      <div
        v-else
        class="space-y-4"
      >
        <UForm
          :state="passwordForm"
          class="space-y-4"
          @submit.prevent="handlePasswordReset"
        >
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
              :disabled="isResetting"
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
              :disabled="isResetting"
            />
          </UFormField>

          <div class="flex flex-col gap-3 pt-2">
            <UButton
              type="submit"
              size="lg"
              class="w-full"
              :loading="isResetting"
              :disabled="isResetting || !isPasswordFormValid"
            >
              Reset Password
            </UButton>

            <NuxtLink
              to="/"
              class="text-center text-sm text-muted hover:text-primary transition-colors"
            >
              Back to Login
            </NuxtLink>
          </div>
        </UForm>
      </div>
    </UCard>
  </div>
</template>
