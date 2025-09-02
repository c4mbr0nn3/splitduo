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
        <USlideover title="Slideover with title">
          <UButton
            variant="ghost"
            size="sm"
            icon="i-lucide-menu"
            @click="isMenuOpen = true"
          />

          <template #body>
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
                </div>
              </template>

              <!-- Mobile navigation items -->
              <nav class="flex-1">
                <div class="space-y-1">
                  <NuxtLink
                    v-for="item in navigationItems"
                    :key="item.to"
                    :to="item.to"
                    class="flex items-center px-4 py-3 text-base font-medium rounded-md transition-colors"
                    :class="isActivePage(item.to)
                      ? 'bg-emerald-50 text-emerald-700 border-l-4 border-emerald-500'
                      : 'text-gray-700 hover:bg-gray-50'"
                    @click="isMenuOpen = false"
                  >
                    <UIcon
                      :name="item.icon"
                      class="w-6 h-6 mr-3"
                      :class="isActivePage(item.to) ? 'text-emerald-500' : 'text-gray-400'"
                    />
                    {{ item.label }}
                  </NuxtLink>
                </div>
              </nav>

              <!-- Mobile user actions -->
              <template #footer>
                <div class="border-t border-gray-200 pt-4">
                  <UButton
                    variant="outline"
                    block
                    size="lg"
                    :loading="isLoggingOut"
                    @click="handleLogout"
                  >
                    <UIcon
                      name="i-lucide-log-out"
                      class="w-5 h-5 mr-2"
                    />
                    Logout
                  </UButton>
                </div>
              </template>
            </UCard>
          </template>
        </USlideover>
      </div>
    </div>
  </header>
</template>

<script setup>
const { user, logout } = useAuth()
const { showSuccess } = useNotifications()
const route = useRoute()

const isMenuOpen = ref(false)
const isLoggingOut = ref(false)

const handleLogout = async () => {
  isLoggingOut.value = true
  isMenuOpen.value = false

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

const isActivePage = (path) => {
  if (path === '/dashboard') {
    return route.path === '/dashboard' || route.path === '/'
  }
  return route.path.startsWith(path)
}

// Close mobile menu when route changes
watch(() => route.path, () => {
  isMenuOpen.value = false
})
</script>
