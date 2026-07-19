<script setup>
const route = useRoute()
const { validateInvitationToken, acceptInvitation } = useInvitations()

const token = ref(route.query.token || '')

const isValidating = ref(true)
const isTokenValid = ref(false)
const invitationData = ref(null)
const isSubmitting = ref(false)
const acceptSuccess = ref(false)

const form = ref({
  firstName: '',
  lastName: '',
  password: '',
  confirmPassword: '',
})

const passwordRules = computed(() => {
  const password = form.value.password
  if (!password) return null
  return {
    length: password.length >= 8,
    uppercase: /[A-Z]/.test(password),
    lowercase: /[a-z]/.test(password),
    digit: /[0-9]/.test(password),
    special: /[!@#$%^&*()_+\-=[\]{}|;:,.<>?]/.test(password),
  }
})

const passwordValidationError = computed(() => {
  if (!form.value.password) return null
  const rules = passwordRules.value
  if (!rules) return null
  const errors = []
  if (!rules.length) errors.push('at least 8 characters')
  if (!rules.uppercase) errors.push('one uppercase letter')
  if (!rules.lowercase) errors.push('one lowercase letter')
  if (!rules.digit) errors.push('one digit')
  if (!rules.special) errors.push('one special character')
  return errors.length > 0 ? `Password must contain ${errors.join(', ')}` : null
})

const confirmPasswordError = computed(() => {
  if (!form.value.confirmPassword) return null
  if (form.value.password !== form.value.confirmPassword) {
    return 'Passwords do not match'
  }
  return null
})

const isFormValid = computed(() => {
  return (
    form.value.firstName.trim()
    && form.value.lastName.trim()
    && form.value.password
    && form.value.confirmPassword
    && !passwordValidationError.value
    && !confirmPasswordError.value
  )
})

const validateToken = async () => {
  if (!token.value) {
    isValidating.value = false
    isTokenValid.value = false
    return
  }

  try {
    const data = await validateInvitationToken(token.value)
    invitationData.value = data
    isTokenValid.value = true
  }
  catch {
    isTokenValid.value = false
  }
  finally {
    isValidating.value = false
  }
}

const handleSubmit = async () => {
  if (!isFormValid.value || isSubmitting.value) return

  isSubmitting.value = true

  try {
    await acceptInvitation({
      token: token.value,
      firstName: form.value.firstName,
      lastName: form.value.lastName,
      password: form.value.password,
      confirmPassword: form.value.confirmPassword,
    })

    acceptSuccess.value = true

    setTimeout(() => {
      navigateTo('/')
    }, 2000)
  }
  catch {
    // Error shown via toast
  }
  finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  validateToken()
})

useHead({
  title: 'Accept Invitation',
})

definePageMeta({
  layout: 'auth',
})
</script>

<template>
  <div class="min-h-dvh flex items-center justify-center p-4">
    <UCard class="w-full max-w-md">
      <template #header>
        <UiCardHeader
          title="Create Your Account"
          :subtitle="invitationData ? `Join ${invitationData.groupName} on SplitDuo` : 'SplitDuo'"
        />
      </template>

      <!-- Validating Token -->
      <div
        v-if="isValidating"
        class="flex flex-col items-center justify-center py-8 space-y-4"
      >
        <USkeleton class="h-8 w-8 rounded-full" />
        <p class="text-sm text-muted">
          Validating invitation...
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
            Invalid or Expired Invitation
          </h4>
          <p class="text-sm text-muted">
            This invitation link is invalid or has expired.
          </p>
          <p class="text-sm text-muted">
            Please ask the group admin to send a new invitation.
          </p>
        </div>

        <div class="flex justify-center pt-4">
          <NuxtLink
            to="/"
            class="text-sm text-muted hover:text-primary transition-colors"
          >
            Go to Login
          </NuxtLink>
        </div>
      </div>

      <!-- Success -->
      <div
        v-else-if="acceptSuccess"
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
            Account Created!
          </h4>
          <p class="text-sm text-muted">
            Your account has been created successfully.
          </p>
          <p class="text-sm text-muted">
            Redirecting to login...
          </p>
        </div>
      </div>

      <!-- Registration Form -->
      <div
        v-else
        class="space-y-4"
      >
        <UForm
          :state="form"
          class="space-y-4"
          @submit.prevent="handleSubmit"
        >
          <UFormField
            label="Email"
            name="email"
          >
            <UInput
              :model-value="invitationData?.email"
              type="email"
              disabled
              class="w-full"
            />
          </UFormField>

          <UFormField
            label="First Name"
            name="firstName"
            required
          >
            <UInput
              v-model="form.firstName"
              placeholder="Enter your first name"
              required
              class="w-full"
              :disabled="isSubmitting"
            />
          </UFormField>

          <UFormField
            label="Last Name"
            name="lastName"
            required
          >
            <UInput
              v-model="form.lastName"
              placeholder="Enter your last name"
              required
              class="w-full"
              :disabled="isSubmitting"
            />
          </UFormField>

          <UFormField
            label="Password"
            name="password"
            required
            :error="passwordValidationError"
          >
            <UInput
              v-model="form.password"
              type="password"
              placeholder="Create a password"
              autocomplete="new-password"
              required
              class="w-full"
              :disabled="isSubmitting"
            />
            <template
              v-if="passwordRules"
              #help
            >
              <div class="text-xs mt-1 space-y-0.5">
                <p :class="passwordRules.length ? 'text-success' : 'text-muted'">
                  {{ passwordRules.length ? '\u2713' : '\u2022' }} At least 8 characters
                </p>
                <p :class="passwordRules.uppercase ? 'text-success' : 'text-muted'">
                  {{ passwordRules.uppercase ? '\u2713' : '\u2022' }} One uppercase letter
                </p>
                <p :class="passwordRules.lowercase ? 'text-success' : 'text-muted'">
                  {{ passwordRules.lowercase ? '\u2713' : '\u2022' }} One lowercase letter
                </p>
                <p :class="passwordRules.digit ? 'text-success' : 'text-muted'">
                  {{ passwordRules.digit ? '\u2713' : '\u2022' }} One digit
                </p>
                <p :class="passwordRules.special ? 'text-success' : 'text-muted'">
                  {{ passwordRules.special ? '\u2713' : '\u2022' }} One special character
                </p>
              </div>
            </template>
          </UFormField>

          <UFormField
            label="Confirm Password"
            name="confirmPassword"
            required
            :error="confirmPasswordError"
          >
            <UInput
              v-model="form.confirmPassword"
              type="password"
              placeholder="Confirm your password"
              autocomplete="new-password"
              required
              class="w-full"
              :disabled="isSubmitting"
            />
          </UFormField>

          <div class="flex flex-col gap-3 pt-2">
            <UButton
              type="submit"
              size="lg"
              class="w-full"
              :loading="isSubmitting"
              :disabled="isSubmitting || !isFormValid"
            >
              Create Account
            </UButton>

            <NuxtLink
              to="/"
              class="text-center text-sm text-muted hover:text-primary transition-colors"
            >
              Already have an account? Log in
            </NuxtLink>
          </div>
        </UForm>
      </div>
    </UCard>
  </div>
</template>
