<script setup lang="ts">
import type { ValidateInvitationResponse } from '~/types/domain'

const { t, locale } = useI18n()
const route = useRoute()
const { validateInvitationToken, acceptInvitation } = useInvitations()

const token = ref(typeof route.query.token === 'string' ? route.query.token : '')

const isValidating = ref(true)
const isTokenValid = ref(false)
const invitationData = ref<ValidateInvitationResponse | null>(null)
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
  if (!form.value.password) return undefined
  const rules = passwordRules.value
  if (!rules) return undefined
  const errors = []
  if (!rules.length) errors.push(t('auth.atLeast8Chars'))
  if (!rules.uppercase) errors.push(t('auth.oneUppercase'))
  if (!rules.lowercase) errors.push(t('auth.oneLowercase'))
  if (!rules.digit) errors.push(t('auth.oneDigit'))
  if (!rules.special) errors.push(t('auth.oneSpecialChar'))
  return errors.length > 0 ? t('auth.passwordMustContain', { errors: errors.join(', ') }) : undefined
})

const confirmPasswordError = computed(() => {
  if (!form.value.confirmPassword) return undefined
  if (form.value.password !== form.value.confirmPassword) {
    return t('auth.passwordsDoNotMatch')
  }
  return undefined
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
    invitationData.value = data ?? null
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
      uiLanguage: locale.value,
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
  title: computed(() => t('auth.createAccount')),
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
          :title="$t('auth.createYourAccount')"
          :subtitle="invitationData ? $t('auth.joinGroupOnSplitDuo', { groupName: invitationData.groupName }) : 'SplitDuo'"
        />
      </template>

      <!-- Validating Token -->
      <div
        v-if="isValidating"
        class="flex flex-col items-center justify-center py-8 space-y-4"
      >
        <USkeleton class="h-8 w-8 rounded-full" />
        <p class="text-sm text-muted">
          {{ $t('auth.validatingInvitation') }}
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
            {{ $t('auth.invalidOrExpiredInvitation') }}
          </h4>
          <p class="text-sm text-muted">
            {{ $t('auth.invitationLinkExpired') }}
          </p>
          <p class="text-sm text-muted">
            {{ $t('auth.askAdminNewInvitation') }}
          </p>
        </div>

        <div class="flex justify-center pt-4">
          <NuxtLink
            to="/"
            class="text-sm text-muted hover:text-primary transition-colors"
          >
            {{ $t('auth.goToLogin') }}
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
            class="size-12 text-success"
          />
        </div>

        <div class="text-center space-y-2">
          <h4 class="text-lg font-semibold">
            {{ $t('auth.accountCreated') }}
          </h4>
          <p class="text-sm text-muted">
            {{ $t('auth.accountCreatedSuccess') }}
          </p>
          <p class="text-sm text-muted">
            {{ $t('auth.redirectingToLogin') }}
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
            :label="$t('auth.email')"
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
            :label="$t('auth.firstName')"
            name="firstName"
            required
          >
            <UInput
              v-model="form.firstName"
              :placeholder="$t('auth.enterFirstName')"
              required
              class="w-full"
              :disabled="isSubmitting"
            />
          </UFormField>

          <UFormField
            :label="$t('auth.lastName')"
            name="lastName"
            required
          >
            <UInput
              v-model="form.lastName"
              :placeholder="$t('auth.enterLastName')"
              required
              class="w-full"
              :disabled="isSubmitting"
            />
          </UFormField>

          <UFormField
            :label="$t('auth.password')"
            name="password"
            required
            :error="passwordValidationError"
          >
            <UInput
              v-model="form.password"
              type="password"
              :placeholder="$t('auth.createPassword')"
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
                  {{ passwordRules.length ? '\u2713' : '\u2022' }} {{ $t('auth.atLeast8Chars') }}
                </p>
                <p :class="passwordRules.uppercase ? 'text-success' : 'text-muted'">
                  {{ passwordRules.uppercase ? '\u2713' : '\u2022' }} {{ $t('auth.oneUppercase') }}
                </p>
                <p :class="passwordRules.lowercase ? 'text-success' : 'text-muted'">
                  {{ passwordRules.lowercase ? '\u2713' : '\u2022' }} {{ $t('auth.oneLowercase') }}
                </p>
                <p :class="passwordRules.digit ? 'text-success' : 'text-muted'">
                  {{ passwordRules.digit ? '\u2713' : '\u2022' }} {{ $t('auth.oneDigit') }}
                </p>
                <p :class="passwordRules.special ? 'text-success' : 'text-muted'">
                  {{ passwordRules.special ? '\u2713' : '\u2022' }} {{ $t('auth.oneSpecialChar') }}
                </p>
              </div>
            </template>
          </UFormField>

          <UFormField
            :label="$t('auth.confirmPassword')"
            name="confirmPassword"
            required
            :error="confirmPasswordError"
          >
            <UInput
              v-model="form.confirmPassword"
              type="password"
              :placeholder="$t('auth.confirmYourPassword')"
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
              {{ $t('auth.createAccount') }}
            </UButton>

            <NuxtLink
              to="/"
              class="text-center text-sm text-muted hover:text-primary transition-colors"
            >
              {{ $t('auth.alreadyHaveAccount') }}
            </NuxtLink>
          </div>
        </UForm>
      </div>
    </UCard>
  </div>
</template>
