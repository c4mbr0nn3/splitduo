<template>
  <UHeader
    title="SplitDuo"
    to="/dashboard"
    mode="drawer"
  >
    <UNavigationMenu

      :items="navigationItems"
    />
    <template #right>
      <div class="flex items-center gap-2">
        <UColorModeButton />
        <UButton
          variant="ghost"
          color="error"
          icon="i-lucide-log-out"
          label="Logout"
          :loading="isLoggingOut"
          class="hidden md:inline-flex"
          @click="handleLogout"
        />
      </div>
    </template>

    <template #body>
      <UCard
        class="flex flex-col flex-1"
        variant="soft"
        :ui="{ body: { base: 'flex-1' } }"
      >
        <template #header>
          <div class="flex items-center">
            <UAvatar
              icon="i-lucide-user"
              size="xl"
            />
            <div class="ml-3">
              <div class="text-base font-medium text-primary">
                {{ user?.firstName || 'User' }} {{ user?.lastName || '' }}
              </div>
              <div class="text-sm text-dimmed">
                {{ user?.email || '' }}
              </div>
            </div>
          </div>
        </template>
        <UNavigationMenu
          variant="link"
          orientation="vertical"
          :items="navigationItems"
          :ui="{ list: 'flex flex-col space-y-3' }"
        />
        <template #footer>
          <div class="flex flex-col space-y-4">
            <ButtonColorMode />
            <UButton
              variant="ghost"
              color="error"
              icon="i-lucide-log-out"
              label="Logout"
              :loading="isLoggingOut"
              @click="handleLogout"
            />
          </div>
        </template>
      </UCard>
    </template>
  </UHeader>
</template>

<script setup>
const { user, isGlobalAdmin, logout } = useAuth()
const { showSuccess } = useNotifications()

const route = useRoute()

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

const navigationItems = computed(() => {
  const items = [
    {
      to: '/dashboard',
      label: 'Dashboard',
      icon: 'i-lucide-home',
      active: route.path.startsWith('/dashboard'),
    },
    {
      to: '/groups',
      label: 'Groups',
      icon: 'i-lucide-users',
      active: route.path.startsWith('/groups'),
    },
    {
      to: '/profile',
      label: 'Profile',
      icon: 'i-lucide-heart',
      active: route.path.startsWith('/profile'),
    },
  ]

  // Add admin menu for admin users
  if (isGlobalAdmin.value) {
    items.push({
      to: '/admin/users',
      label: 'Admin',
      icon: 'i-lucide-shield-user',
      active: route.path.startsWith('/admin'),
    })
  }

  return items
})
</script>
