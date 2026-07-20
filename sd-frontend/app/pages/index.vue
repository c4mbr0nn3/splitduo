<template>
  <div class="min-h-dvh flex items-center justify-center p-4">
    <div class="w-full max-w-md">
      <UCard>
        <UAuthForm
          title="Welcome Back"
          :fields="fields"
          :submit="{ label: 'Login', loading: isLoading }"
          @submit="onSubmit"
        />
        <template #footer>
          <div class="text-center">
            <NuxtLink
              to="/forgot-password"
              class="text-sm text-muted hover:text-primary transition-colors"
            >
              Forgot your password?
            </NuxtLink>
          </div>
        </template>
      </UCard>
    </div>
  </div>
</template>

<script setup>
useHead({
  title: 'Login',
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

const fields = [
  {
    name: 'email',
    type: 'email',
    label: 'Email',
    placeholder: 'Enter your email',
    required: true,
    size: 'lg',
  },
  {
    name: 'password',
    type: 'password',
    label: 'Password',
    placeholder: 'Enter your password',
    required: true,
    size: 'lg',
  },
]

const { login, isLoading } = useAuth()
const { showError, showSuccess } = useNotifications()

async function onSubmit(event) {
  const { data } = event
  if (!data.email || !data.password) {
    showError('Please fill in all fields')
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
        showSuccess('Login successful! Redirecting...')
        await navigateTo('/dashboard')
      }
    }
    else {
      showError(result.error || 'Login failed')
    }
  }
  catch (error) {
    showError(error.message || 'An unexpected error occurred')
  }
}
</script>
