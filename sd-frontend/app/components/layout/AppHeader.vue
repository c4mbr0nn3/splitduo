<template>
  <header class="sticky top-0 z-50">
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
      <div class="flex justify-between items-center h-16">
        <!-- Mobile menu button -->
        <!-- Logo/Brand -->
        <NuxtLink
          to="/dashboard"
          class="flex items-center"
        >
          <div class="text-2xl font-bold">
            SplitDuo
          </div>
        </NuxtLink>
        <UModal>
          <UButton
            variant="ghost"
            size="sm"
            icon="i-lucide-menu"
          />

          <template #content>
            <UCard
              class="flex flex-col flex-1"
              :ui="{ body: { base: 'flex-1' } }"
            >
              <template #header>
                <!-- Mobile user info -->
                <div class="flex items-center">
                  <UAvatar
                    icon="i-lucide-user"
                    size="xl"
                  />
                  <div class="ml-3">
                    <div class="text-base font-medium">
                      {{ user?.firstName || 'User' }} {{ user?.lastName || '' }}
                    </div>
                    <div class="text-sm text-gray-500">
                      {{ user?.email || '' }}
                    </div>
                  </div>
                  <UButton
                    variant="ghost"
                    color="neutral"
                    icon="i-lucide-log-out"
                    :loading="isLoggingOut"
                    class="ml-auto"
                    @click="handleLogout"
                  />
                </div>
              </template>
              <!-- Mobile navigation items -->
              <UNavigationMenu
                variant="link"
                orientation="vertical"
                color="secondary"
                :items="navigationItems"
                :ui="{ list: 'flex flex-col space-y-3' }"
              />
              <!-- Mobile user actions -->
              <template #footer>
                <ButtonColorMode />
              </template>
            </UCard>
          </template>
        </UModal>
      </div>
    </div>
  </header>
</template>

<script setup>
const { user, logout } = useAuth()
const { showSuccess } = useNotifications()

const isLoggingOut = ref(false)

const handleLogout = async () => {
  isLoggingOut.value = true

  try {
    await logout()
    showSuccess('Logged out successfully')
    await navigateTo('/login')
  }
  catch (error) {
    console.error('Logout failed:', error)
  }
  finally {
    isLoggingOut.value = false
  }
}

const navigationItems = [
  {
    to: '/dashboard',
    label: 'Dashboard',
    icon: 'i-lucide-home',
  },
  {
    to: '/dashboard',
    label: 'Groups',
    icon: 'i-lucide-users',
  },
  {
    to: '/dashboard',
    label: 'Expenses',
    icon: 'i-lucide-receipt',
  },
  {
    to: '/dashboard',
    label: 'Profile',
    icon: 'i-lucide-user',
  },
  {
    to: '/dashboard',
    label: 'Settings',
    icon: 'i-lucide-settings',
  },
]

const userMenuItems = [
  [{
    label: 'Profile',
    icon: 'i-lucide-user',
    click: () => navigateTo('/profile'),
  }],
  [{
    label: 'Settings',
    icon: 'i-lucide-settings',
    click: () => navigateTo('/settings'),
  }],
  [{
    label: 'Logout',
    icon: 'i-lucide-log-out',
    click: handleLogout,
  }],
]
</script>
