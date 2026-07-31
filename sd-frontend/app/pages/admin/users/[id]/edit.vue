<template>
  <AdminUserForm
    title="Edit User"
    submit-label="Update"
    :initial-data="initialData"
    :loading="isLoading"
    :is-edit="true"
    @submit="onSubmit"
    @cancel="goBack"
  />
</template>

<script setup>
const route = useRoute()
const { goBack } = useSmartBack('/admin/users')
const userId = route.params.id

const { fetchUser, updateUser, isLoading } = useUsers()

const currentUser = ref(null)

const initialData = computed(() => ({
  firstName: currentUser.value?.firstName || '',
  lastName: currentUser.value?.lastName || '',
  email: currentUser.value?.email || '',
  globalRoleId: currentUser.value?.globalRoleId || UserRole.BASE_USER,
}))

async function onSubmit(formData) {
  try {
    const { globalRoleId, ...rest } = formData
    const updatedUser = await updateUser(userId, { ...rest, globalRole: globalRoleId })
    if (updatedUser) {
      navigateTo('/admin/users')
    }
  }
  catch (err) {
    console.error('Error updating user:', err)
  }
}

onMounted(async () => {
  if (userId) {
    try {
      currentUser.value = await fetchUser(userId)
    }
    catch (error) {
      console.error('Failed to load user:', error)
      navigateTo('/admin/users')
    }
  }
})

useHead({
  title: computed(() => `Edit ${currentUser.value?.firstName || 'User'}`),
})

definePageMeta({
  middleware: ['auth', 'admin'],
})
</script>
