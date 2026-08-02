<template>
  <AdminUserForm
    :title="$t('admin.editUser')"
    :submit-label="$t('admin.update')"
    :initial-data="initialData"
    :loading="isLoading"
    :is-edit="true"
    @submit="onSubmit"
    @cancel="goBack"
  />
</template>

<script setup lang="ts">
import type { User } from '~/types/domain'

interface UserFormData {
  firstName: string
  lastName: string
  email: string
  globalRoleId: number
}

const { t } = useI18n()
const route = useRoute()
const { goBack } = useSmartBack('/admin/users')
const userId = String(route.params.id)

const { fetchUser, updateUser, isLoading } = useUsers()

const currentUser = ref<User | null>(null)

const initialData = computed<UserFormData>(() => ({
  firstName: currentUser.value?.firstName || '',
  lastName: currentUser.value?.lastName || '',
  email: currentUser.value?.email || '',
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

onMounted(async () => {
  if (userId) {
    try {
      const user = await fetchUser(userId)
      currentUser.value = user ?? null
    }
    catch (error: unknown) {
      console.error('Failed to load user:', error)
      navigateTo('/admin/users')
    }
  }
})

useHead({
  title: computed(() => `${t('admin.editUser')} - ${currentUser.value?.firstName || ''}`),
})

definePageMeta({
  middleware: ['auth', 'admin'],
})
</script>
