<template>
  <div class="min-h-dvh flex items-center justify-center p-4">
    <div class="w-full max-w-md">
      <UCard>
        <UAuthForm
          :title="$t('auth.welcomeBack')"
          :fields="fields"
          :submit="{ label: $t('auth.login'), loading: isLoading }"
          @submit="onSubmit"
        />
        <template #footer>
          <div class="text-center">
            <NuxtLink
              to="/forgot-password"
              class="text-sm text-muted hover:text-primary transition-colors"
            >
              {{ $t('auth.forgotPassword') }}
            </NuxtLink>
          </div>
        </template>
      </UCard>
    </div>
  </div>
</template>

<script setup>
const { t } = useI18n()

useHead({
  title: computed(() => t('auth.login')),
})

definePageMeta({
  layout: 'auth',
  middleware: defineNuxtRouteMiddleware(() => {
    const { user } = useAuth()
    if (user.value) {
      return navigateTo('/dashboard')
    }
  }),
})

const fields = computed(() => [
  {
    name: 'email',
    type: 'email',
    label: t('auth.email'),
    placeholder: t('auth.enterEmail'),
    required: true,
    size: 'lg',
  },
  {
    name: 'password',
    type: 'password',
    label: t('auth.password'),
    placeholder: t('auth.enterPassword'),
    required: true,
    size: 'lg',
  },
])

const { login, isLoading } = useAuth()
const { showError, showSuccess } = useNotifications()

async function onSubmit(event) {
  const { data } = event
  if (!data.email || !data.password) {
    showError(t('auth.fillAllFields'))
    return
  }

  try {
    const result = await login({
      email: data.email,
      password: data.password,
    })

    if (result.success) {
      if (result.requiresTwoFactor) {
        await navigateTo('/auth/verify')
      }
      else {
        showSuccess(t('auth.loginSuccessful'))
        await navigateTo('/dashboard')
      }
    }
    else {
      showError(result.error || t('auth.loginFailed'))
    }
  }
  catch (error) {
    showError(error.message || t('auth.unexpectedError'))
  }
}
</script>
