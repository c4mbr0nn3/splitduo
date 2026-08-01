<script setup>
defineProps({
  members: {
    type: Array,
    required: true,
  },
  isLoading: {
    type: Boolean,
    default: false,
  },
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
