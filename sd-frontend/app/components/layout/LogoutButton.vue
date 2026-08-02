<template>
  <UButton
    variant="ghost"
    color="error"
    icon="i-lucide-log-out"
    :label="$t('auth.logout')"
    :loading="isLoggingOut"
    @click="handleLogout"
  />
</template>

<script setup lang="ts">
const { t } = useI18n()
const { logout } = useAuth()
const { showSuccess } = useNotifications()

const isLoggingOut = ref(false)

const handleLogout = async () => {
  isLoggingOut.value = true

  try {
    await logout()
    showSuccess(t('auth.logout'))
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
