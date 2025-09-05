<template>
  <header class="sticky top-0 z-50 bg-default">
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
      <div class="flex justify-between items-center h-16">
        <NuxtLink
          to="/dashboard"
          class="flex items-center"
        >
          <div class="text-2xl font-bold text-primary">
            SplitDuo
          </div>
        </NuxtLink>
        <UModal
          v-model:open="isMobileMenuOpen"
          :ui="{
            content: 'top-0 left-1/2 -translate-x-1/2 translate-y-4 w-[calc(100vw-2rem)] max-w-lg max-h-[calc(100dvh-2rem)] sm:max-h-[calc(100dvh-4rem)] rounded-lg shadow-lg ring ring-default overflow-hidden',
          }"
        >
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
                  <UButton
                    variant="ghost"
                    color="neutral"
                    icon="i-lucide-x"
                    class="ml-auto"
                    @click="isMobileMenuOpen = false"
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
        </UModal>
      </div>
    </div>
  </header>
</template>

<script setup>
const { user, logout } = useAuth()
const { showSuccess } = useNotifications()
const route = useRoute()

const isMobileMenuOpen = ref(false)
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
    to: '/profile',
    label: 'Profile',
    icon: 'i-lucide-heart',
  },
]

watch(() => route.path, () => {
  isMobileMenuOpen.value = false
})
</script>
