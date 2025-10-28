<script setup>
const api = useApi()
const { showSuccess } = useNotifications()

const isSubmitting = ref(false)
const emailSent = ref(false)

const form = ref({
  email: '',
})

const emailError = computed(() => {
  if (!form.value.email) return null
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  return !emailRegex.test(form.value.email) ? 'Please enter a valid email address' : null
})

const isFormValid = computed(() => {
  return form.value.email && !emailError.value
})

const handleSubmit = async () => {
  if (!isFormValid.value || isSubmitting.value) return

  isSubmitting.value = true

  try {
    await api.post('/auth/forgot-password', {
      email: form.value.email,
    })

    emailSent.value = true
    showSuccess('Password reset instructions have been sent to your email.')
  }
  catch {
    // Backend returns success even if email doesn't exist (security)
    // So we show success regardless
    emailSent.value = true
    showSuccess('If your email is registered, you will receive password reset instructions.')
  }
  finally {
    isSubmitting.value = false
  }
}

useHead({
  title: 'Forgot Password',
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
          title="Forgot Password?"
          subtitle="Enter your email to receive reset instructions"
        />
      </template>
      <div v-if="!emailSent">
        <UForm
          :state="form"
          class="space-y-4"
          @submit.prevent="handleSubmit"
        >
          <UFormField
            label="Email Address"
            name="email"
            required
            :error="emailError"
          >
            <UInput
              v-model="form.email"
              type="email"
              placeholder="Enter your email"
              autocomplete="email"
              required
              :disabled="isSubmitting"
              class="w-full"
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
              Send Reset Link
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

      <div
        v-else
        class="space-y-4"
      >
        <div class="flex justify-center">
          <UIcon
            name="i-lucide-mail-check"
            class="size-16 text-success"
          />
        </div>

        <div class="text-center space-y-2">
          <h4 class="font-semibold">
            Check Your Email
          </h4>
          <p class="text-sm text-muted">
            We've sent password reset instructions to <strong>{{ form.email }}</strong>
          </p>
          <p class="text-sm text-muted">
            The link will expire in 1 hour.
          </p>
        </div>

        <div class="flex flex-col gap-2 pt-4">
          <UButton
            size="lg"
            variant="outline"
            class="w-full"
            @click="() => { emailSent = false; form.email = '' }"
          >
            Try Another Email
          </UButton>

          <NuxtLink
            to="/"
            class="text-center text-sm text-muted hover:text-primary transition-colors"
          >
            Back to Login
          </NuxtLink>
        </div>
      </div>
    </UCard>
  </div>
</template>
