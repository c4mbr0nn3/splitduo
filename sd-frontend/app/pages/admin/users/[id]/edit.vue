<template>
  <div>
    <div class="flex justify-center px-4 pt-6 sm:pt-8">
      <div class="w-full max-w-2xl">
        <div class="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-3">
          <div class="flex flex-col gap-1 min-w-0">
            <span class="text-sm font-medium text-muted">{{ $t('admin.email') }}</span>
            <span class="text-highlighted truncate">{{ currentUser?.email }}</span>
            <UBadge
              v-if="currentUser?.pendingEmail"
              color="warning"
              variant="subtle"
              class="w-fit mt-1"
            >
              {{ $t('admin.pendingEmail', { email: currentUser.pendingEmail }) }}
            </UBadge>
          </div>
          <UButton
            :label="$t('admin.changeEmail')"
            variant="outline"
            @click="isEmailModalOpen = true"
          />
        </div>
      </div>
    </div>

    <AdminUserForm
      :title="$t('admin.editUser')"
      :submit-label="$t('admin.update')"
      :initial-data="initialData"
      :loading="isLoading"
      :is-edit="true"
      @submit="onSubmit"
      @cancel="goBack"
    />

    <AdminChangeEmailModal
      v-model:open="isEmailModalOpen"
      :user-id="userId"
      :current-email="currentUser?.email || ''"
      :pending-email="currentUser?.pendingEmail"
      @success="reloadUser"
    />
  </div>
</template>

<script setup lang="ts">
import type { User } from '~/types/domain'

interface UserFormData {
  firstName: string
  lastName: string
  globalRoleId: number
}

const { t } = useI18n()
const route = useRoute()
const { goBack } = useSmartBack('/admin/users')
const userId = String(route.params.id)

const { fetchUser, updateUser, isLoading } = useUsers()

const currentUser = ref<User | null>(null)
const isEmailModalOpen = ref(false)

const initialData = computed<UserFormData>(() => ({
  firstName: currentUser.value?.firstName || '',
  lastName: currentUser.value?.lastName || '',
  globalRoleId: Number(currentUser.value?.globalRoleId) || UserRole.BASE_USER,
}))

async function onSubmit(formData: UserFormData) {
  try {
    const { globalRoleId, ...rest } = formData
    const updatedUser = await updateUser(userId, { ...rest, globalRole: globalRoleId })
    if (updatedUser) {
      navigateTo('/admin/users')
    }
  }
  catch (err: unknown) {
    console.error('Error updating user:', err)
  }
}

async function fetchCurrentUser() {
  if (!userId) return
  try {
    const user = await fetchUser(userId)
    currentUser.value = user ?? null
  }
  catch (error: unknown) {
    console.error('Failed to load user:', error)
    navigateTo('/admin/users')
  }
}

// Re-fetch after a successful email-change request so the pending
// verification badge becomes visible right away
async function reloadUser() {
  try {
    const user = await fetchUser(userId)
    currentUser.value = user ?? null
  }
  catch (error: unknown) {
    console.error('Failed to reload user:', error)
  }
}

onMounted(fetchCurrentUser)

useHead({
  title: computed(() => `${t('admin.editUser')} - ${currentUser.value?.firstName || ''}`),
})

definePageMeta({
  middleware: ['auth', 'admin'],
})
</script>
