<template>
  <div class="space-y-4">
    <UFormField :label="$t('groups.emailAddress')">
      <div class="flex gap-2">
        <UInput
          v-model="email"
          type="email"
          :placeholder="$t('groups.emailPlaceholder')"
          class="w-full"
          @keydown.enter.prevent="onInvite"
        />
        <UButton
          icon="i-lucide-send"
          :label="$t('groups.invite')"
          :loading="isLoading"
          :disabled="!isValidEmail"
          @click="onInvite"
        />
      </div>
    </UFormField>
  </div>
</template>

<script setup>
const props = defineProps({
  groupId: {
    type: String,
    required: true,
  },
})

const emit = defineEmits(['success'])

const { sendInvitation, isLoading } = useInvitations()

const email = ref('')

const isValidEmail = computed(() => {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value)
})

const onInvite = async () => {
  if (!isValidEmail.value) return

  try {
    const result = await sendInvitation(props.groupId, email.value)
    email.value = ''
    emit('success', result)
  }
  catch {
    // Error already shown via toast in composable
  }
}
</script>
