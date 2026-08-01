<script setup>
const { locale, setLocale } = useI18n()
const settings = useUserSettings()
const { refreshToken } = useAuth()

const languageItems = [
  { label: 'English', value: 'en' },
  { label: 'Italiano', value: 'it' },
]

const selectedLanguage = computed({
  get() {
    return locale.value
  },
  async set(newLocale) {
    setLocale(newLocale)
    await settings.update({ uiLanguage: newLocale })
    // Trigger JWT refresh so subsequent requests carry the new lang claim
    await refreshToken()
  },
})
</script>

<template>
  <USelect
    v-model="selectedLanguage"
    :items="languageItems"
    :placeholder="$t('profile.language')"
    value-key="value"
    class="w-full sm:w-64"
  />
</template>
