<template>
  <GroupsGroupForm
    :title="$t('groups.createTitle')"
    :submit-label="$t('groups.createSubmit')"
    :loading="isLoading"
    @submit="onSubmit"
    @cancel="onCancel"
  />
</template>

<script setup lang="ts">
// Script-side usage of a component requires an explicit import (auto-import
// registration only covers template usage).
import PushOptInModal from '~/components/push/PushOptInModal.vue'

interface GroupFormData {
  name: string
  description: string
  useAliases: boolean
}

const { t } = useI18n()
const { createGroup, isLoading } = useGroups()
const push = usePush()
const pushOverlay = useOverlay().create(PushOptInModal)

async function onSubmit(formData: GroupFormData) {
  try {
    const group = await createGroup(formData)
    if (group) {
      // Contextual push opt-in right after the group exists (user gesture
      // chain intact) — navigation waits for the modal to close.
      if (push.shouldShowPushOptIn()) {
        await pushOverlay.open({ groupName: group.name })
      }
      navigateTo(`/groups/${group.id}`)
    }
  }
  catch (err: unknown) {
    console.error('Error creating group:', err)
  }
}

const { goBack } = useSmartBack('/groups')

function onCancel() {
  goBack()
}

useHead({
  title: computed(() => t('groups.createTitle')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
