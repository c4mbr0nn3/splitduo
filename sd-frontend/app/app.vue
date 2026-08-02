<script setup lang="ts">
import { en, it } from '@nuxt/ui/locale'

const { locale } = useI18n()
const uiLocales: Record<string, typeof en> = { en, it }
const uiLocale = computed(() => uiLocales[locale.value] ?? en)
</script>

<template>
  <UApp :locale="uiLocale">
    <NuxtLoadingIndicator
      color="repeating-linear-gradient(to right, #14b8a6 0%, #5eead4 50%, #0d9488 100%)"
      :height="3"
      :throttle="100"
      :duration="1500"
      error-color="repeating-linear-gradient(to right, #f43f5e 0%, #fb7185 50%, #e11d48 100%)"
    />
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
    <PwaUpdate />
    <PwaInstall />
    <PwaOffline />
  </UApp>
</template>
