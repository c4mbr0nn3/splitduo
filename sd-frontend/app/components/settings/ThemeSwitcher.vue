<script setup>
const { t } = useI18n()
const { settings } = useUserSettings()
const colorMode = useColorMode()

const themeItems = computed(() => [
  { label: t('profile.auto'), value: 'auto' },
  { label: t('profile.light'), value: 'light' },
  { label: t('profile.dark'), value: 'dark' },
])

const selectedTheme = computed({
  get() {
    return settings.value.theme
  },
  set(value) {
    colorMode.preference = value === 'auto' ? 'system' : value
  },
})
</script>

<template>
  <USelect
    v-model="selectedTheme"
    :items="themeItems"
    :placeholder="$t('profile.selectTheme')"
    value-key="value"
    class="w-full sm:w-64"
  />
</template>
