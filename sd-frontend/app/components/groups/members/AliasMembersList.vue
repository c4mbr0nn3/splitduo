<template>
  <div class="space-y-6">
    <!-- Finalize banner -->
    <UCard
      v-if="isGroupAdmin && !group?.aliasSetupFinalized"
      variant="soft"
      color="warning"
      :ui="{ body: 'p-4 sm:p-5' }"
    >
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="flex items-start gap-3">
          <UIcon
            name="i-lucide-alert-triangle"
            class="size-6 text-warning shrink-0"
          />
          <div>
            <p class="font-semibold text-highlighted">
              {{ $t('groups.finalizeAliasSetup') }}
            </p>
            <p class="text-sm text-muted mt-1">
              {{ $t('groups.finalizeAliasDescription') }}
            </p>
            <p
              v-if="!hasMultiPersonAlias"
              class="text-sm text-warning mt-1"
            >
              {{ $t('groups.createMultiPersonAlias') }}
            </p>
          </div>
        </div>
        <UButton
          :label="$t('groups.finalizeButton')"
          color="warning"
          variant="solid"
          :loading="isFinalizing"
          :disabled="!hasMultiPersonAlias || isFinalizing"
          @click="$emit('finalize')"
        />
      </div>
    </UCard>

    <!-- Aliases -->
    <div class="space-y-4">
      <UCard
        v-for="alias in aliasesWithMembers"
        :key="alias.id"
        variant="outline"
        class="sd-surface"
      >
        <div class="space-y-4">
          <div class="flex items-start justify-between gap-3">
            <div class="flex items-center gap-2 flex-wrap min-w-0">
              <UIcon
                name="i-lucide-layers"
                class="size-5 text-secondary shrink-0"
              />
              <h4 class="text-base font-semibold text-highlighted truncate">
                {{ alias.name }}
              </h4>
              <UBadge
                v-if="alias.isSingleton"
                variant="soft"
                color="secondary"
                :label="$t('members.singleton')"
                size="xs"
              />
            </div>
            <div
              v-if="isGroupAdmin"
              class="flex items-center gap-2 shrink-0"
            >
              <UButton
                icon="i-lucide-pencil"
                variant="ghost"
                size="sm"
                square
                @click="$emit('rename', alias)"
              />
              <UButton
                icon="i-lucide-trash-2"
                variant="ghost"
                color="error"
                size="sm"
                square
                :loading="aliasLoading"
                @click="$emit('delete', alias)"
              />
            </div>
          </div>

          <div class="space-y-1">
            <p
              v-if="!alias.members?.length"
              class="text-sm text-muted"
            >
              {{ $t('members.noMembers') }}
            </p>
            <GroupsMembersRow
              v-for="member in alias.members"
              :key="member.id"
              :member="member"
            >
              <template
                v-if="isGroupAdmin"
                #actions
              >
                <span
                  v-if="member.id !== currentUserId"
                  @click.stop
                >
                  <UiButtonDropdown
                    :items="getRoleItems(member)"
                    icon-only
                    dropdown-icon="i-lucide-ellipsis-vertical"
                    size="sm"
                    square
                    variant="ghost"
                    color="neutral"
                  />
                </span>
                <span @click.stop>
                  <UButton
                    icon="i-lucide-user-minus"
                    variant="ghost"
                    color="neutral"
                    size="sm"
                    square
                    :loading="aliasLoading"
                    @click="$emit('remove', alias, member.id)"
                  />
                </span>
              </template>
            </GroupsMembersRow>
          </div>

          <UFormField
            v-if="isGroupAdmin && availableMembers.length"
            :label="$t('groups.addMember')"
            :name="`assign-${alias.id}`"
          >
            <div class="flex items-center gap-2">
              <USelect
                :model-value="selectedAssignee[alias.id]"
                :items="availableMembers"
                value-key="value"
                label-key="label"
                :placeholder="$t('groups.selectMember')"
                class="w-full"
                @update:model-value="value => selectedAssignee[alias.id] = value"
              />
              <UButton
                icon="i-lucide-user-plus"
                variant="outline"
                size="sm"
                square
                :loading="aliasLoading"
                :disabled="!selectedAssignee[alias.id]"
                @click="onAssign(alias)"
              />
            </div>
          </UFormField>
        </div>
      </UCard>
    </div>

    <!-- Empty aliases state -->
    <UiEmptyState
      v-if="!aliases.length && !aliasLoading"
      icon="i-lucide-users-round"
      :title="$t('groups.noAliasesYet')"
      :subtitle="$t('groups.noAliasesSubtitle')"
    />

    <!-- Pending Invitations -->
    <div
      v-if="isGroupAdmin && pendingInvitations.length"
      class="space-y-2"
    >
      <USeparator />
      <h3 class="text-sm font-medium text-muted">
        {{ $t('members.pendingInvitations') }}
      </h3>
      <GroupsMembersPendingInvitationCard
        v-for="invitation in pendingInvitations"
        :key="invitation.id"
        :invitation="invitation"
        :invitation-loading="invitationLoading"
        @resend="$emit('resend', $event)"
        @revoke="$emit('revoke', $event)"
      />
    </div>
  </div>
</template>

<script setup>
const { t } = useI18n()
const props = defineProps({
  aliases: {
    type: Array,
    required: true,
  },
  members: {
    type: Array,
    required: true,
  },
  group: {
    type: Object,
    default: null,
  },
  isGroupAdmin: {
    type: Boolean,
    default: false,
  },
  currentUserId: {
    type: String,
    default: '',
  },
  groupId: {
    type: String,
    default: '',
  },
  aliasLoading: {
    type: Boolean,
    default: false,
  },
  pendingInvitations: {
    type: Array,
    default: () => [],
  },
  invitationLoading: {
    type: Boolean,
    default: false,
  },
  isFinalizing: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['rename', 'delete', 'assign', 'remove', 'finalize', 'resend', 'revoke', 'refresh'])

const { changeMemberRole } = useGroups()
const modal = useModal()

const getRoleItems = (member) => {
  const items = []

  if (member.role === 'member') {
    items.push({
      label: t('members.promoteToAdmin'),
      icon: 'i-lucide-arrow-up-circle',
      color: 'success',
      onSelect: () => confirmRoleChange(member, 'admin'),
    })
  }
  else if (member.role === 'admin') {
    items.push({
      label: t('members.demoteToMember'),
      icon: 'i-lucide-arrow-down-circle',
      color: 'warning',
      onSelect: () => confirmRoleChange(member, 'member'),
    })
  }

  return items
}

const confirmRoleChange = async (member, newRole) => {
  const isPromote = newRole === 'admin'
  const firstName = member.firstName || member.fullName || ''

  const confirmed = await modal.warning({
    title: isPromote ? t('members.promoteConfirmTitle') : t('members.demoteConfirmTitle'),
    content: isPromote
      ? t('members.promoteConfirmContent', { name: firstName })
      : t('members.demoteConfirmContent', { name: firstName }),
    confirmText: isPromote ? t('members.promote') : t('members.demote'),
    cancelText: t('common.cancel'),
  })

  if (!confirmed) return

  try {
    await changeMemberRole(props.groupId, member.id, newRole)
    emit('refresh')
  }
  catch {
    // Error shown via toast
  }
}

const selectedAssignee = reactive({})

const memberMap = computed(() => {
  const map = new Map()
  for (const member of props.members) {
    map.set(member.id, member)
  }
  return map
})

const aliasesWithMembers = computed(() => {
  return props.aliases.map((alias) => {
    const enriched = (alias.members || []).map((m) => {
      const rich = memberMap.value.get(m.id)
      return rich ? { ...m, ...rich } : m
    })
    return { ...alias, members: enriched }
  })
})

const hasMultiPersonAlias = computed(() => {
  return props.aliases.some(a => (a.members?.length || 0) >= 2)
})

const assignedIds = computed(() => {
  return new Set(props.aliases.flatMap(a => a.members?.map(m => m.id) || []))
})

const availableMembers = computed(() => {
  return props.members
    .filter(m => !assignedIds.value.has(m.id))
    .map(m => ({
      value: m.id,
      label: m.fullName || `${m.firstName} ${m.lastName || ''}`.trim(),
    }))
})

const onAssign = (alias) => {
  const userId = selectedAssignee[alias.id]
  if (!userId) return
  emit('assign', alias, userId)
  selectedAssignee[alias.id] = null
}
</script>
