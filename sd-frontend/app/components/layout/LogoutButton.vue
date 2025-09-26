<template>
  <UButton
    variant="ghost"
    color="error"
    icon="i-lucide-log-out"
    label="Logout"
    :loading="isLoggingOut"
    @click="handleLogout"
  />
</template>

<script setup>
const { logout } = useAuth()
const { showSuccess } = useNotifications()

const isLoggingOut = ref(false)

const handleLogout = async () => {
  isLoggingOut.value = true

  try {
    await logout()
    showSuccess('Logged out successfully')
    await navigateTo('/')
  }
  catch (error) {
    console.error('Logout failed:', error)
  }
  finally {
    isLoggingOut.value = false
  }
}
</script>
