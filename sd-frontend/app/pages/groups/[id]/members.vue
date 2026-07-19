<template>
  <div class="py-8">
    <UiLoadingSpinner
      v-if="pageLoading"
      text="Loading members..."
    />

    <UiEmptyState
      v-else-if="loadError"
      icon="i-lucide-users"
      title="Unable to load members"
    >
      <template #action>
        <UButton
          color="primary"
          variant="outline"
          size="sm"
          @click="retryLoad"
        >
          Retry
        </UButton>
      </template>
    </UiEmptyState>

    <UCard v-else>
      <template #header>
        <div class="flex items-center justify-between">
          <UiCardHeader
            title="Members"
            :subtitle="group?.name"
            :back-to="`/groups/${groupId}`"
          />
          <UBadge
            v-if="members.length"
            variant="soft"
            icon="i-lucide-users"
            :label="members.length"
          />
        </div>
      </template>

      <div class="space-y-6">
        <GroupsMembersList
          :members="members"
          :is-loading="isLoading"
        />

        <!-- Pending Invitations (admin only) -->
        <div
          v-if="isGroupAdmin && pendingInvitations.length"
          class="space-y-2"
        >
          <USeparator />
          <h3 class="text-sm font-medium text-muted">
            Pending Invitations
          </h3>
          <UCard
            v-for="invitation in pendingInvitations"
            :key="invitation.id"
            variant="outline"
          >
            <div class="flex items-center justify-between">
              <div>
                <p class="font-semibold">
                  {{ invitation.email }}
                </p>
                <p class="text-sm text-muted">
                  Invited {{ formatDate(invitation.invitedAt) }}
                </p>
              </div>
              <div class="flex items-center gap-3">
                <UBadge
                  variant="soft"
                  color="warning"
                  label="Pending"
                  icon="i-lucide-clock"
                />
                <UButton
                  icon="i-lucide-refresh-cw"
                  variant="ghost"
                  size="sm"
                  square
                  :loading="invitationLoading"
                  @click="onResend(invitation)"
                />
                <UButton
                  icon="i-lucide-x"
                  variant="ghost"
                  color="error"
                  size="sm"
                  square
                  :loading="invitationLoading"
                  @click="onRevoke(invitation)"
                />
              </div>
            </div>
          </UCard>
        </div>
      </div>

      <template #footer>
        <div
          v-if="isGroupAdmin"
          class="flex justify-end"
        >
          <UButton
            icon="i-lucide-user-plus"
            label="Invite User"
            class="w-full sm:w-auto"
            @click="navigateToInvite"
          />
        </div>
      </template>
    </UCard>
  </div>
</template>

<script setup>
import { formatDate } from '~/utils/date'

const route = useRoute()
const groupId = route.params.id

const { user } = useAuth()
const { currentGroup, fetchGroup, fetchGroupMembers, isLoading } = useGroups()
const { fetchGroupInvitations, resendInvitation, revokeInvitation, isLoading: invitationLoading } = useInvitations()

const group = computed(() => currentGroup.value)
const members = ref([])
const pendingInvitations = ref([])
const pageLoading = ref(true)
const loadError = ref(false)

const isGroupAdmin = computed(() => {
  return members.value.some(m => m.id === user.value?.id && m.role === 'admin')
})

const navigateToInvite = () => {
  navigateTo(`/groups/${groupId}/invite`)
}

const onResend = async (invitation) => {
  try {
    const updated = await resendInvitation(groupId, invitation.id)
    const index = pendingInvitations.value.findIndex(i => i.id === invitation.id)
    if (index !== -1 && updated) {
      pendingInvitations.value[index] = updated
    }
  }
  catch {
    // Error shown via toast
  }
}

const modal = useModal()

const onRevoke = async (invitation) => {
  const confirmed = await modal.warning({
    title: 'Revoke invitation',
    subtitle: 'This cannot be undone.',
    content: `Revoke the invitation for '${invitation.email}'?`,
    confirmText: 'Revoke',
    cancelText: 'Cancel',
  })

  if (!confirmed) return

  try {
    await revokeInvitation(groupId, invitation.id)
    pendingInvitations.value = pendingInvitations.value.filter(i => i.id !== invitation.id)
  }
  catch {
    // Error shown via toast
  }
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

const loadInvitations = async () => {
  try {
    const data = await fetchGroupInvitations(groupId)
    pendingInvitations.value = data || []
  }
  catch {
    pendingInvitations.value = []
  }
}

const retryLoad = async () => {
  loadError.value = false
  pageLoading.value = true
  try {
    await Promise.all([
      fetchGroup(groupId),
      loadGroupMembers(),
    ])
    if (isGroupAdmin.value) {
      await loadInvitations()
    }
  }
  catch {
    loadError.value = true
  }
  finally {
    pageLoading.value = false
  }
}

onMounted(async () => {
  if (groupId) {
    try {
      await Promise.all([
        fetchGroup(groupId),
        loadGroupMembers(),
      ])
      // Load invitations after members so we know if current user is admin
      if (isGroupAdmin.value) {
        await loadInvitations()
      }
    }
    catch {
      loadError.value = true
    }
    finally {
      pageLoading.value = false
    }
  }
})

useHead({
  title: computed(() => `Members - ${group.value?.name || 'Group'}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
