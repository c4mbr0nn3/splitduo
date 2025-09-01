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
        @submit="onSubmit"
      >
        <div class="grid grid-cols-1 gap-4 mb-4">
          <UFormGroup
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
          </UFormGroup>
          <UFormGroup
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
          </UFormGroup>
        </div>
        <UButton
          type="submit"
          label="Login"
          block
          size="lg"
          :disabled="loading"
          :loading="loading"
        />
      </UForm>
    </UCard>
  </div>
</template>

<script setup>
import { ref } from 'vue'

const form = ref({
  email: '',
  password: '',
})
const loading = ref(false)

function onSubmit() {
  loading.value = true
  // Simulate login process
  setTimeout(() => {
    loading.value = false
    // Handle login logic here
    // e.g., call API, show error, redirect, etc.
  }, 1200)
}
</script>
