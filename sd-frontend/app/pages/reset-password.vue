<script setup>
const { t } = useI18n()
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
    showError(t('auth.invalidResetToken'))
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
    const errorMessage = error?.data?.error?.message || t('auth.invalidExpiredToken')
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
    showSuccess(t('auth.passwordResetSuccessful'))

    // Redirect to login after 2 seconds
    setTimeout(() => {
      navigateTo('/')
    }, 2000)
  }
  catch (error) {
    const errorMessage = error?.data?.error?.message || t('auth.failedToResetPassword')
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
  title: computed(() => t('auth.resetPassword')),
})

definePageMeta({
  layout: 'auth',
})
</script>

<template>
  <div class="min-h-dvh flex items-center justify-center p-4">
    <UCard class="w-full max-w-md sd-surface">
      <template #header>
        <UiCardHeader
          :title="$t('auth.resetYourPassword')"
          :subtitle="$t('auth.createNewPassword')"
        />
      </template>

      <!-- Validating Token -->
      <div
        v-if="isValidating"
        class="flex flex-col items-center justify-center py-8 space-y-4"
      >
        <USkeleton class="h-8 w-8 rounded-full" />
        <p class="text-sm text-muted">
          {{ $t('auth.validatingResetToken') }}
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
            class="size-12 text-error"
          />
        </div>

        <div class="text-center space-y-2">
          <h4 class="text-lg font-semibold">
            {{ $t('auth.invalidOrExpiredLink') }}
          </h4>
          <p class="text-sm text-muted">
            {{ $t('auth.resetLinkExpired') }}
          </p>
          <p class="text-sm text-muted">
            {{ $t('auth.resetLinkExpiry') }}
          </p>
        </div>

        <div class="flex flex-col gap-2 pt-4">
          <NuxtLink to="/forgot-password">
            <UButton
              size="lg"
              class="w-full"
            >
              {{ $t('auth.requestNewLink') }}
            </UButton>
          </NuxtLink>

          <NuxtLink
            to="/"
            class="text-center text-sm text-muted hover:text-primary transition-colors"
          >
            {{ $t('auth.backToLogin') }}
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
            class="size-12 text-success"
          />
        </div>

        <div class="text-center space-y-2">
          <h4 class="text-lg font-semibold">
            {{ $t('auth.passwordResetSuccessful') }}
          </h4>
          <p class="text-sm text-muted">
            {{ $t('auth.passwordUpdated') }}
          </p>
          <p class="text-sm text-muted">
            {{ $t('auth.redirectingToLogin') }}
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
            :label="$t('auth.newPassword')"
            name="newPassword"
            required
            :error="passwordValidationError"
          >
            <UInput
              v-model="passwordForm.newPassword"
              type="password"
              :placeholder="$t('auth.enterNewPassword')"
              autocomplete="new-password"
              required
              class="w-full"
              :disabled="isResetting"
            />
            <template #help>
              <p class="text-xs text-muted mt-1">
                {{ $t('auth.passwordRequirements') }}
              </p>
            </template>
          </UFormField>

          <UFormField
            :label="$t('auth.confirmNewPassword')"
            name="confirmPassword"
            required
            :error="confirmPasswordError"
          >
            <UInput
              v-model="passwordForm.confirmPassword"
              type="password"
              :placeholder="$t('auth.confirmNewPasswordPlaceholder')"
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
              {{ $t('auth.resetPassword') }}
            </UButton>

            <NuxtLink
              to="/"
              class="text-center text-sm text-muted hover:text-primary transition-colors"
            >
              {{ $t('auth.backToLogin') }}
            </NuxtLink>
          </div>
        </UForm>
      </div>
    </UCard>
  </div>
</template>
