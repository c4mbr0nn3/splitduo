<script setup lang="ts">
const { t } = useI18n()
const api = useApi()
const { showSuccess } = useNotifications()

const isSubmitting = ref(false)
const emailSent = ref(false)

const form = ref({
  email: '',
})

const emailError = computed(() => {
  if (!form.value.email) return undefined
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  return !emailRegex.test(form.value.email) ? t('auth.validEmailRequired') : undefined
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
    showSuccess(t('auth.resetInstructionsSentToEmail'))
  }
  catch {
    // Backend returns success even if email doesn't exist (security)
    // So we show success regardless
    emailSent.value = true
    showSuccess(t('auth.resetInstructionsSentGeneric'))
  }
  finally {
    isSubmitting.value = false
  }
}

useHead({
  title: computed(() => t('auth.forgotPassword')),
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
          :title="$t('auth.forgotPasswordTitle')"
          :subtitle="$t('auth.forgotPasswordSubtitle')"
        />
      </template>
      <div v-if="!emailSent">
        <UForm
          :state="form"
          class="space-y-4"
          @submit.prevent="handleSubmit"
        >
          <UFormField
            :label="$t('auth.emailAddress')"
            name="email"
            required
            :error="emailError"
          >
            <UInput
              v-model="form.email"
              type="email"
              :placeholder="$t('auth.enterEmail')"
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
              {{ $t('auth.sendResetLink') }}
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

      <div
        v-else
        class="space-y-4"
      >
        <div class="flex justify-center">
          <UIcon
            name="i-lucide-mail-check"
            class="size-12 text-success"
          />
        </div>

        <div class="text-center space-y-2">
          <h4 class="text-lg font-semibold">
            {{ $t('auth.checkYourEmail') }}
          </h4>
          <p class="text-sm text-muted">
            <span>{{ $t('auth.resetInstructionsSent', { email: form.email }) }}</span>
          </p>
          <p class="text-sm text-muted">
            {{ $t('auth.linkExpiresIn1Hour') }}
          </p>
        </div>

        <div class="flex flex-col gap-2 pt-4">
          <UButton
            size="lg"
            variant="outline"
            class="w-full"
            @click="() => { emailSent = false; form.email = '' }"
          >
            {{ $t('auth.tryAnotherEmail') }}
          </UButton>

          <NuxtLink
            to="/"
            class="text-center text-sm text-muted hover:text-primary transition-colors"
          >
            {{ $t('auth.backToLogin') }}
          </NuxtLink>
        </div>
      </div>
    </UCard>
  </div>
</template>
