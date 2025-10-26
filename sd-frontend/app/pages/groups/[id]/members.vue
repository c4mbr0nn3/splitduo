<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <div class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h1 class="text-xl font-bold text-primary">
                Members
              </h1>
              <p
                v-if="group"
                class="text-sm text-muted mt-1"
              >
                {{ group.name }}
              </p>
            </div>
            <UBadge
              v-if="members.length"
              variant="soft"
              icon="i-lucide-users"
              :label="members.length"
            />
          </div>
        </div>
      </template>
      <UiLoadingSpinner
        v-if="isLoading"
        text="Loading members..."
      />
      <div
        v-else-if="members.length"
        class="space-y-2"
      >
        <UCard
          v-for="member in members"
          :key="member.id"
          variant="outline"
        >
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-3">
              <div>
                <p class="font-semibold">
                  {{ member.fullName || `${member.firstName} ${member.lastName || ''}`.trim() }}
                </p>
                <p class="text-sm text-muted">
                  {{ member.email }}
                </p>
              </div>
            </div>
            <UBadge
              variant="soft"
              :color="member.role === 'admin' ? 'success' : 'primary'"
              :label="member.role"
              :icon="member.role === 'admin' ? 'i-lucide-crown' : 'i-lucide-user'"
              class="capitalize"
            />
          </div>
        </UCard>
      </div>
      <UiEmptyState
        v-else
        icon="i-lucide-users"
        title="No members yet"
        subtitle="Invite users to join this group"
      />
      <template #footer>
        <div class="flex justify-end gap-3">
          <UButton
            variant="ghost"
            label="Back to Group"
            @click="navigateToGroup"
          />
          <UButton
            icon="i-lucide-user-plus"
            label="Invite Users"
            @click="navigateToInvite"
          />
        </div>
      </template>
    </UCard>
  </div>
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id

const { currentGroup, fetchGroup, fetchGroupMembers, isLoading } = useGroups()

const group = computed(() => currentGroup.value)
const members = ref([])

const navigateToGroup = () => {
  navigateTo(`/groups/${groupId}`)
}

const navigateToInvite = () => {
  navigateTo(`/groups/${groupId}/invite`)
}

const loadGroupMembers = async () => {
  try {
    const data = await fetchGroupMembers(groupId)
    members.value = data.map((item) => {
      return {
        ...item.user,
        role: item.role || 'member',
      }
    })
  }
  catch (error) {
    console.error('Failed to load group members:', error)
    members.value = []
  }
}

onMounted(async () => {
  if (groupId) {
    await Promise.all([
      fetchGroup(groupId),
      loadGroupMembers(),
    ])
  }
})

useHead({
  title: computed(() => `Members - ${group.value?.name || 'Group'}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
