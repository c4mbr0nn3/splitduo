<template>
  <div class="min-h-dvh flex items-center justify-center p-4">
    <div class="sd-stagger w-full max-w-md flex flex-col items-center gap-6">
      <h1 class="sr-only">
        {{ $t('auth.welcomeBack') }}
      </h1>

      <NuxtLink
        to="/"
        aria-label="SplitDuo"
        class="group flex items-center justify-center rounded-2xl p-2 transition duration-300 ease-out hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      >
        <img
          src="/logo.svg"
          alt="SplitDuo"
          width="56"
          height="56"
          class="h-14 w-14 rounded-xl shadow-sm ring-1 ring-primary/10 transition duration-300 ease-out group-hover:shadow-md group-hover:ring-primary/20"
        >
      </NuxtLink>

      <UCard
        class="w-full transition duration-300 ease-out hover:-translate-y-0.5"
        :ui="{
          root: 'rounded-2xl bg-default ring-1 ring-default shadow-[var(--sd-card-shadow)] hover:shadow-[var(--sd-card-shadow-hover)]',
          header: 'p-0',
          body: 'p-5 sm:p-7',
          footer: 'p-5 sm:px-7 sm:pb-6',
        }"
      >
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
              class="group inline-flex items-center justify-center gap-1.5 text-sm text-muted hover:text-primary transition-colors"
            >
              {{ $t('auth.forgotPassword') }}
              <UIcon
                name="i-lucide-arrow-right"
                class="h-4 w-4 transition-transform duration-200 group-hover:translate-x-0.5"
                aria-hidden="true"
              />
            </NuxtLink>
          </div>
        </template>
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
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
    size: 'lg' as const,
  },
  {
    name: 'password',
    type: 'password',
    label: t('auth.password'),
    placeholder: t('auth.enterPassword'),
    required: true,
    size: 'lg' as const,
  },
])

const { login, isLoading } = useAuth()
const { showError, showSuccess } = useNotifications()

async function onSubmit(event: { data: Record<string, unknown> }) {
  const { data } = event
  const email = String(data.email || '')
  const password = String(data.password || '')
  if (!email || !password) {
    showError(t('auth.fillAllFields'))
    return
  }

  try {
    const result = await login({
      email,
      password,
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
  catch (error: unknown) {
    showError(error instanceof Error ? error.message : t('auth.unexpectedError'))
  }
}
</script>
