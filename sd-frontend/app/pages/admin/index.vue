<template>
  <div class="py-6 sm:py-8">
    <UiCardHeader
      size="lg"
      :title="$t('admin.title')"
      class="mb-6"
    >
      <template #actions>
        <UBadge
          color="neutral"
          variant="subtle"
        >
          v{{ appVersion }}
        </UBadge>
      </template>
    </UiCardHeader>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <NuxtLink
        v-for="card in adminCards"
        :key="card.to"
        :to="card.to"
        class="group block rounded-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      >
        <UCard
          class="sd-surface sd-surface-hover h-full"
          :ui="{ body: 'p-5 sm:p-6' }"
        >
          <div class="flex items-start gap-4">
            <div class="size-11 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <UIcon
                :name="card.icon"
                class="size-5.5 text-primary"
              />
            </div>
            <div class="min-w-0 flex-1">
              <h2 class="text-lg font-semibold text-highlighted">
                {{ $t(card.titleKey) }}
              </h2>
              <p class="text-sm text-muted mt-1">
                {{ $t(card.subtitleKey) }}
              </p>
            </div>
            <UIcon
              name="i-lucide-chevron-right"
              class="size-5 text-dimmed mt-2 transition-transform duration-200 group-hover:translate-x-1"
            />
          </div>
        </UCard>
      </NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
const { t } = useI18n()

const appVersion = computed(() => useRuntimeConfig().public.appVersion)

definePageMeta({
  middleware: ['auth', 'admin'],
  layout: 'default',
})

const adminCards = [
  {
    to: '/admin/users',
    icon: 'i-lucide-users',
    titleKey: 'admin.users',
    subtitleKey: 'admin.usersCardSubtitle',
  },
  {
    to: '/admin/ai',
    icon: 'i-lucide-sparkles',
    titleKey: 'admin.aiCardTitle',
    subtitleKey: 'admin.aiCardSubtitle',
  },
] as const

useHead({
  title: computed(() => t('admin.title')),
})
</script>
