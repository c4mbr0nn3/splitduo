export default function useUsers() {
  const api = useApi()
  const { t } = useI18n()
  const { showError, showSuccess } = useNotifications()

  const users = ref([])
  const currentUser = ref(null)
  const userImports = ref([])
  const userStats = ref(null)
  const isLoading = ref(false)

  // Get all users (admin only)
  const fetchUsers = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/users')
      if (response.success && response.data) {
        users.value = response.data
      }
    }
    catch (error) {
      showError(t('toasts.users.loadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Get current user profile
  const fetchCurrentUser = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/users/me')
      if (response.success && response.data) {
        currentUser.value = response.data
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.users.profileLoadFailed'))
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  // Update current user profile
  const updateCurrentUser = async (userData) => {
    try {
      const response = await api.put('/users/me', userData)
      if (response.success && response.data) {
        currentUser.value = response.data
        showSuccess(t('toasts.users.profileUpdated'))
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.users.profileUpdateFailed'))
      throw error
    }
  }

  // Change password
  const changePassword = async (passwordData) => {
    try {
      await api.put('/users/me/password', passwordData)
      showSuccess(t('toasts.users.passwordChanged'))
    }
    catch (error) {
      showError(t('toasts.users.passwordChangeFailed'))
      throw error
    }
  }

  // Get user imports
  const fetchUserImports = async () => {
    try {
      const response = await api.get('/users/me/imports')
      if (response.success && response.data) {
        userImports.value = response.data
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.users.importsLoadFailed'))
      throw error
    }
  }

  // Get user stats
  const fetchUserStats = async () => {
    try {
      const response = await api.get('/users/me/stats')
      if (response.success && response.data) {
        userStats.value = response.data
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.users.statsLoadFailed'))
      throw error
    }
  }

  // Get user by ID
  const fetchUser = async (userId) => {
    try {
      const response = await api.get(`/users/${userId}`)
      if (response.success && response.data) {
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.users.loadOneFailed'))
      throw error
    }
  }

  // Update user (admin only)
  const updateUser = async (userId, userData) => {
    try {
      const response = await api.put(`/users/${userId}`, userData)
      if (response.success && response.data) {
        const index = users.value.findIndex(u => u.id === userId)
        if (index !== -1) {
          users.value[index] = response.data
        }
        showSuccess(t('toasts.users.updated'))
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.users.updateFailed'))
      throw error
    }
  }

  // Delete user (admin only)
  const deleteUser = async (userId) => {
    try {
      await api.delete(`/users/${userId}`)
      users.value = users.value.filter(u => u.id !== userId)
      showSuccess(t('toasts.users.deleted'))
    }
    catch (error) {
      showError(t('toasts.users.deleteFailed'))
      throw error
    }
  }

  // Change user role (admin only)
  const changeUserRole = async (userId, globalRoleId) => {
    try {
      const response = await api.put(`/users/${userId}`, { globalRole: globalRoleId })
      if (response.success && response.data) {
        const index = users.value.findIndex(u => u.id === userId)
        if (index !== -1) {
          users.value[index] = response.data
        }
        showSuccess(t('toasts.users.roleUpdated'))
        return response.data
      }
    }
    catch (error) {
      showError(t('toasts.users.roleUpdateFailed'))
      throw error
    }
  }

  // Revoke all tokens for user (admin only)
  const revokeUserTokens = async (userGuid) => {
    try {
      await api.post(`/auth/${userGuid}/revoke`)
      showSuccess(t('toasts.users.tokensRevoked'))
    }
    catch (error) {
      showError(t('toasts.users.tokensRevokeFailed'))
      throw error
    }
  }

  return {
    users: readonly(users),
    currentUser: readonly(currentUser),
    userImports: readonly(userImports),
    userStats: readonly(userStats),
    isLoading: readonly(isLoading),
    fetchUsers,
    fetchCurrentUser,
    updateCurrentUser,
    changePassword,
    fetchUserImports,
    fetchUserStats,
    fetchUser,
    updateUser,
    changeUserRole,
    deleteUser,
    revokeUserTokens,
  }
}
