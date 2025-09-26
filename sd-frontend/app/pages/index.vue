<template>
  <div class="flex justify-center items-center h-screen p-4">
    <UCard class="w-full">
      <UAuthForm
        title="Welcome Back"
        :fields="fields"
        :submit="{ label: 'Login', loading: isLoading }"
        @submit="onSubmit"
      />
    </UCard>
  </div>
</template>

<script setup>
useHead({
  title: 'Login',
})

definePageMeta({
  layout: 'auth',
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
      showSuccess('Login successful! Redirecting...')
      await navigateTo('/dashboard')
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
