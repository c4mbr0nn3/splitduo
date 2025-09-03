<template>
  <div class="flex justify-center items-center h-screen p-4">
    <UCard class="w-full">
      <template #header>
        <div class="text-2xl">
          Welcome Back
        </div>
      </template>
      <UForm
        :state="form"
        @submit.prevent="onSubmit"
      >
        <div class="grid grid-cols-1 gap-4 mb-4">
          <UFormField
            label="Email"
            name="email"
            required
          >
            <UInput
              v-model="form.email"
              type="email"
              placeholder="Enter your email"
              required
              class="w-full"
              size="lg"
            />
          </UFormField>
          <UFormField
            label="Password"
            name="password"
            required
          >
            <UInput
              v-model="form.password"
              type="password"
              placeholder="Enter your password"
              required
              class="w-full"
              size="lg"
            />
          </UFormField>
        </div>
        <UButton
          type="submit"
          label="Login"
          block
          size="lg"
          :disabled="isLoading"
          :loading="isLoading"
        />
      </UForm>
    </UCard>
  </div>
</template>

<script setup>
definePageMeta({
  layout: 'auth',
})

const form = ref({
  email: '',
  password: '',
})

const { login, isLoading } = useAuth()
const { showError, showSuccess } = useNotifications()

async function onSubmit() {
  if (!form.value.email || !form.value.password) {
    showError('Please fill in all fields')
    return
  }

  try {
    const result = await login({
      email: form.value.email,
      password: form.value.password,
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
