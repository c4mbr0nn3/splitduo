<template>
  <UHeader
    title="SplitDuo"
    to="/dashboard"
    mode="drawer"
    class="shadow-xs"
  >
    <UNavigationMenu

      :items="navigationItems"
    />
    <template #right>
      <div class="flex items-center gap-2">
        <UColorModeButton />
        <LayoutLogoutButton class="hidden md:inline-flex" />
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
            <LayoutLogoutButton />
          </div>
        </template>
      </UCard>
    </template>
  </UHeader>
</template>

<script setup>
const { user, isGlobalAdmin } = useAuth()

const route = useRoute()

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
