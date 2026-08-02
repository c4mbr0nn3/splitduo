<template>
  <div class="py-6 sm:py-8">
    <UCard>
      <template #header>
        <UiCardHeader
          :title="$t('groups.inviteUserTitle')"
          :subtitle="group?.name"
          :back-to="`/groups/${groupId}/members`"
        />
      </template>

      <UiLoadingSpinner
        v-if="groupLoading"
        :text="$t('groups.loadingGroupDetails')"
      />

      <GroupsInviteUsersForm
        v-else-if="group"
        :group-id="groupId"
        @success="onSuccess"
      />
    </UCard>
  </div>
</template>

<script setup lang="ts">
const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)

const { currentGroup, fetchGroup, isLoading: groupLoading } = useGroups()

const group = computed(() => currentGroup.value)

const onSuccess = async () => {
  await navigateTo(`/groups/${groupId}/members`)
}

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)
  }
})

useHead({
  title: computed(() => `${t('groups.inviteUserTitle')} - ${group.value?.name || ''}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
