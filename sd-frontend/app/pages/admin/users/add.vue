<template>
  <AdminUserForm
    title="Create User"
    submit-label="Create"
    :loading="isLoading"
    @submit="onSubmit"
    @cancel="navigateTo('/admin/users')"
  />
</template>

<script setup>
const { createUser, isLoading } = useUsers()

async function onSubmit(formData) {
  try {
    const user = await createUser(formData)
    if (user) {
      navigateTo('/admin/users')
    }
  }
  catch (err) {
    console.error('Error creating user:', err)
  }
}

useHead({
  title: 'Create User',
})

definePageMeta({
  middleware: ['auth', 'admin'],
})
</script>
