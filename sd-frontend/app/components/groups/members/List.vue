<script setup lang="ts">
interface FlattenedMember {
  id: string
  firstName?: string
  lastName?: string | null
  email?: string
  fullName?: string | null
  role: string
}

interface Props {
  members: FlattenedMember[]
  isLoading?: boolean
}
withDefaults(defineProps<Props>(), {
  isLoading: false,
})
</script>

<template>
  <UiLoadingSpinner
    v-if="isLoading"
    :text="$t('groups.loadingMembers')"
  />
  <div
    v-else
    class="space-y-2"
  >
    <GroupsMembersCard
      v-for="member in members"
      :key="member.id"
      :member="member"
    />
  </div>
</template>
