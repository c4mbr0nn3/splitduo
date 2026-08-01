<template>
  <div class="py-6 sm:py-8">
    <UiLoadingSpinner
      v-if="pageLoading"
      :text="$t('groups.loadingMembers')"
    />

    <UiEmptyState
      v-else-if="loadError"
      icon="i-lucide-users"
      :title="$t('groups.unableToLoadMembers')"
    >
      <template #action>
        <UButton
          color="primary"
          variant="outline"
          size="sm"
          @click="retryLoad"
        >
          {{ $t('groups.retry') }}
        </UButton>
      </template>
    </UiEmptyState>

    <UCard
      v-else
      :ui="{ footer: 'sm:hidden' }"
    >
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <UiCardHeader
            :title="$t('groups.membersTitle')"
            :subtitle="group?.name"
            :back-to="`/groups/${groupId}`"
          />
          <div class="flex items-center gap-2 shrink-0">
            <UButton
              v-if="isGroupAdmin"
              to="invite"
              icon="i-lucide-user-plus"
              :label="$t('groups.invite')"
              class="hidden sm:inline-flex"
            />
            <UButton
              v-if="isGroupAdmin && group?.useAliases"
              icon="i-lucide-plus"
              :label="$t('groups.createAlias')"
              variant="outline"
              class="hidden sm:inline-flex"
              @click="openCreateModal"
            />
            <UBadge
              v-if="members.length"
              variant="soft"
              icon="i-lucide-users"
              :label="members.length"
            />
          </div>
        </div>
      </template>

      <component
        :is="activeComponent"
        :members="members"
        :aliases="aliases"
        :group="group"
        :is-group-admin="isGroupAdmin"
        :current-user-id="user?.id"
        :group-id="groupId"
        :alias-loading="aliasLoading"
        :pending-invitations="pendingInvitations"
        :invitation-loading="invitationLoading"
        :is-loading="isLoading"
        :is-finalizing="isFinalizing"
        @resend="onResend"
        @revoke="onRevoke"
        @rename="openRenameModal"
        @delete="onDelete"
        @assign="onAssignMember"
        @remove="onRemoveMember"
        @finalize="onFinalize"
        @refresh="loadGroupMembers"
      />

      <template #footer>
        <div
          v-if="isGroupAdmin"
          class="flex flex-col gap-2"
        >
          <UButton
            v-if="group?.useAliases"
            icon="i-lucide-plus"
            :label="$t('groups.createAlias')"
            variant="outline"
            class="w-full"
            @click="openCreateModal"
          />
          <UButton
            icon="i-lucide-user-plus"
            :label="$t('groups.inviteUser')"
            class="w-full"
            @click="navigateToInvite"
          />
        </div>
      </template>
    </UCard>

    <!-- Create alias modal -->
    <UModal
      v-if="group?.useAliases"
      v-model:open="isCreateModalOpen"
      :dismissible="!aliasLoading"
    >
      <template #header>
        <UiCardHeader :title="$t('groups.createAlias')" />
      </template>
      <template #body>
        <UForm
          :state="createForm"
          class="space-y-4"
          @submit="onCreate"
        >
          <UFormField
            :label="$t('groups.aliasName')"
            name="name"
            required
          >
            <UInput
              v-model="createForm.name"
              :placeholder="$t('groups.aliasNamePlaceholder')"
              required
              class="w-full"
            />
          </UFormField>
        </UForm>
      </template>
      <template #footer>
        <div class="flex gap-2 w-full">
          <UButton
            color="neutral"
            variant="outline"
            class="ml-auto"
            :disabled="aliasLoading"
            @click="isCreateModalOpen = false"
          >
            {{ $t('common.cancel') }}
          </UButton>
          <UButton
            :loading="aliasLoading"
            :disabled="!createForm.name || aliasLoading"
            @click="onCreate"
          >
            {{ $t('groups.createAlias') }}
          </UButton>
        </div>
      </template>
    </UModal>

    <!-- Rename alias modal -->
    <UModal
      v-if="group?.useAliases"
      v-model:open="isRenameModalOpen"
      :dismissible="!aliasLoading"
    >
      <template #header>
        <UiCardHeader :title="$t('groups.renameAlias')" />
      </template>
      <template #body>
        <UForm
          :state="renameForm"
          class="space-y-4"
          @submit="onRename"
        >
          <UFormField
            :label="$t('groups.aliasName')"
            name="name"
            required
          >
            <UInput
              v-model="renameForm.name"
              :placeholder="$t('groups.enterAliasName')"
              required
              class="w-full"
            />
          </UFormField>
        </UForm>
      </template>
      <template #footer>
        <div class="flex gap-2 w-full">
          <UButton
            color="neutral"
            variant="outline"
            class="ml-auto"
            :disabled="aliasLoading"
            @click="isRenameModalOpen = false"
          >
            {{ $t('common.cancel') }}
          </UButton>
          <UButton
            :loading="aliasLoading"
            :disabled="!renameForm.name || aliasLoading"
            @click="onRename"
          >
            {{ $t('common.save') }}
          </UButton>
        </div>
      </template>
    </UModal>
  </div>
</template>

<script setup>
const { t } = useI18n()
const route = useRoute()
const groupId = route.params.id

const { user } = useAuth()
const { currentGroup, fetchGroup, fetchGroupMembers, isLoading } = useGroups()
const {
  aliases,
  isLoading: aliasLoading,
  fetchAliases,
  createAlias,
  updateAlias,
  deleteAlias,
  assignMember,
  removeMember,
  finalizeAliasSetup,
} = useAliases()
const { fetchGroupInvitations, resendInvitation, revokeInvitation, isLoading: invitationLoading } = useInvitations()

const NormalMembersList = defineAsyncComponent(() => import('~/components/groups/members/NormalMembersList.vue'))
const AliasMembersList = defineAsyncComponent(() => import('~/components/groups/members/AliasMembersList.vue'))

const group = computed(() => currentGroup.value)
const members = ref([])
const pendingInvitations = ref([])
const pageLoading = ref(true)
const loadError = ref(false)
const isFinalizing = ref(false)

const isGroupAdmin = computed(() => {
  return members.value.some(m => m.id === user.value?.id && m.role === 'admin')
})

const activeComponent = computed(() => {
  return group.value?.useAliases ? AliasMembersList : NormalMembersList
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
    title: t('groups.revokeInvitation'),
    subtitle: t('groups.revokeInvitationSubtitle'),
    content: t('groups.revokeInvitationContent', { email: invitation.email }),
    confirmText: t('groups.revoke'),
    cancelText: t('common.cancel'),
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

const refreshAliasesAndMembers = async () => {
  await Promise.all([
    fetchGroup(groupId),
    fetchAliases(groupId),
    loadGroupMembers(),
  ])
}

const retryLoad = async () => {
  loadError.value = false
  pageLoading.value = true
  try {
    await Promise.all([
      fetchGroup(groupId),
      loadGroupMembers(),
    ])
    if (group.value?.useAliases) {
      await fetchAliases(groupId)
    }
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
      // Load aliases for alias-mode groups
      if (group.value?.useAliases) {
        await fetchAliases(groupId)
      }
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

const onFinalize = async () => {
  isFinalizing.value = true
  try {
    await finalizeAliasSetup(groupId)
    await refreshAliasesAndMembers()
  }
  catch {
    // Error shown via toast
  }
  finally {
    isFinalizing.value = false
  }
}

const isCreateModalOpen = ref(false)
const createForm = ref({ name: '' })

const openCreateModal = () => {
  createForm.value.name = ''
  isCreateModalOpen.value = true
}

const onCreate = async () => {
  if (!createForm.value.name) return

  try {
    await createAlias(groupId, { name: createForm.value.name })
    isCreateModalOpen.value = false
    createForm.value.name = ''
    await refreshAliasesAndMembers()
  }
  catch {
    // Error shown via toast
  }
}

const isRenameModalOpen = ref(false)
const renameForm = ref({ name: '' })
const activeAlias = ref(null)

const openRenameModal = (alias) => {
  activeAlias.value = alias
  renameForm.value.name = alias.name
  isRenameModalOpen.value = true
}

const onRename = async () => {
  if (!activeAlias.value || !renameForm.value.name) return

  try {
    await updateAlias(activeAlias.value.id, { name: renameForm.value.name })
    isRenameModalOpen.value = false
    activeAlias.value = null
    renameForm.value.name = ''
    await refreshAliasesAndMembers()
  }
  catch {
    // Error shown via toast
  }
}

const onDelete = async (alias) => {
  const confirmed = await modal.error({
    title: t('groups.deleteAlias'),
    subtitle: t('groups.deleteAliasConfirm'),
    content: t('groups.deleteAliasContent', { name: alias.name }),
    confirmText: t('groups.deleteAliasButton'),
    cancelText: t('common.cancel'),
  })

  if (!confirmed) return

  try {
    await deleteAlias(alias.id)
    await refreshAliasesAndMembers()
  }
  catch {
    // Error shown via toast
  }
}

const onAssignMember = async (alias, userId) => {
  if (!userId) return

  try {
    await assignMember(alias.id, { userId })
    await refreshAliasesAndMembers()
  }
  catch {
    // Error shown via toast
  }
}

const onRemoveMember = async (alias, userId) => {
  const confirmed = await modal.warning({
    title: t('groups.removeMember'),
    subtitle: t('groups.removeMemberConfirm'),
    content: t('groups.removeMemberContent'),
    confirmText: t('groups.remove'),
    cancelText: t('common.cancel'),
  })

  if (!confirmed) return

  try {
    await removeMember(alias.id, userId)
    await refreshAliasesAndMembers()
  }
  catch {
    // Error shown via toast
  }
}

useHead({
  title: computed(() => `${t('groups.membersTitle')} - ${group.value?.name || t('groups.title')}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
