<template>
  <header class="sticky top-0 z-50">
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
      <div class="flex justify-between items-center h-16">
        <!-- Mobile menu button -->
        <div class="flex items-center">
          <UButton
            variant="ghost"
            size="sm"
            class="mr-3 md:hidden"
            @click="isMenuOpen = true"
          >
            <UIcon
              name="i-lucide-menu"
              class="w-6 h-6"
            />
          </UButton>

          <!-- Logo/Brand -->
          <NuxtLink
            to="/"
            class="flex items-center"
          >
            <div class="text-2xl font-bold text-emerald-600">
              SplitDuo
            </div>
          </NuxtLink>
        </div>

        <!-- Desktop Navigation -->
        <nav class="hidden md:flex items-center space-x-8">
          <NuxtLink
            to="/dashboard"
            class="text-gray-700 hover:text-emerald-600 px-3 py-2 text-sm font-medium transition-colors"
            :class="{ 'text-emerald-600 font-semibold': $route.path === '/dashboard' }"
          >
            Dashboard
          </NuxtLink>
          <NuxtLink
            to="/groups"
            class="text-gray-700 hover:text-emerald-600 px-3 py-2 text-sm font-medium transition-colors"
            :class="{ 'text-emerald-600 font-semibold': $route.path.startsWith('/groups') }"
          >
            Groups
          </NuxtLink>
          <NuxtLink
            to="/expenses"
            class="text-gray-700 hover:text-emerald-600 px-3 py-2 text-sm font-medium transition-colors"
            :class="{ 'text-emerald-600 font-semibold': $route.path.startsWith('/expenses') }"
          >
            Expenses
          </NuxtLink>
        </nav>

        <!-- User menu -->
        <div class="flex items-center gap-4">
          <!-- Desktop user info -->
          <div class="hidden sm:flex items-center text-sm text-gray-600">
            <span>Welcome, {{ user?.firstName || 'User' }}</span>
          </div>

          <!-- User menu dropdown -->
          <UDropdown
            :items="userMenuItems"
            :popper="{ placement: 'bottom-end' }"
          >
            <UButton
              variant="ghost"
              size="sm"
              class="flex items-center gap-2"
            >
              <UIcon
                name="i-lucide-user"
                class="w-5 h-5"
              />
              <UIcon
                name="i-lucide-chevron-down"
                class="w-4 h-4"
              />
            </UButton>
          </UDropdown>
        </div>
      </div>
    </div>

    <!-- Mobile Navigation Slideover -->
    <USlideover
      v-model="isMenuOpen"
      side="left"
    >
      <UCard
        class="flex flex-col flex-1"
        :ui="{ body: { base: 'flex-1' } }"
      >
        <template #header>
          <div class="flex items-center justify-between">
            <div class="text-xl font-bold text-emerald-600">
              SplitDuo
            </div>
            <UButton
              variant="ghost"
              size="sm"
              @click="isMenuOpen = false"
            >
              <UIcon
                name="i-lucide-x"
                class="w-5 h-5"
              />
            </UButton>
          </div>
        </template>

        <!-- Mobile user info -->
        <div class="pb-4 mb-4 border-b border-gray-200">
          <div class="flex items-center">
            <div class="w-12 h-12 bg-emerald-100 rounded-full flex items-center justify-center">
              <UIcon
                name="i-lucide-user"
                class="w-6 h-6 text-emerald-600"
              />
            </div>
            <div class="ml-3">
              <div class="text-base font-medium text-gray-900">
                {{ user?.firstName || 'User' }} {{ user?.lastName || '' }}
              </div>
              <div class="text-sm text-gray-500">
                {{ user?.email || '' }}
              </div>
            </div>
          </div>
        </div>

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
    </USlideover>
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
    to: '/groups',
    label: 'Groups',
    icon: 'i-lucide-users',
  },
  {
    to: '/expenses',
    label: 'Expenses',
    icon: 'i-lucide-receipt',
  },
  {
    to: '/profile',
    label: 'Profile',
    icon: 'i-lucide-user',
  },
  {
    to: '/settings',
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
