# SplitDuo Frontend API Composables Structure

## Overview

This document outlines the proposed Nuxt/Vue composables structure for organizing API calls in the SplitDuo frontend application. The structure follows Vue 3 Composition API and Nuxt 3 best practices, providing a clean, maintainable, and scalable approach to API integration.

## Architecture Principles

- **Separation of Concerns**: Each resource has its own composable
- **Reactive State Management**: Vue 3 Composition API with reactive data
- **Centralized Error Handling**: Consistent error handling and user notifications
- **Reusability**: Composables can be used across multiple components
- **Authentication Integration**: Automatic token management and refresh
- **Loading States**: Proper loading indicators for better UX
- **Auto-refresh**: Reactive token refresh on 401 errors

## Directory Structure

```bash
composables/
├── api/
│   ├── base.js                     # Base API configuration and interceptors
│   └── endpoints.js                # API endpoint definitions
├── auth/
│   ├── useAuth.js                  # Authentication composable
│   └── useAuthToken.js             # Token management
├── resources/
│   ├── useUsers.js                 # User operations
│   ├── useGroups.js                # Group operations
│   ├── useExpenses.js              # Expense operations
│   ├── useSettlements.js           # Settlement operations
│   ├── useBalances.js              # Balance operations
│   ├── useCategories.js            # Categories
│   ├── usePaymentModes.js          # Payment modes
│   └── useImportExport.js          # Import/export operations
└── utils/
    ├── useErrorHandling.js         # Error handling utilities
    ├── usePagination.js           # Pagination helpers
    └── useNotifications.js        # Toast/notification management
```

## Core Implementation

### 1. Base API Configuration (`composables/api/base.js`)

```javascript
export function useApi() {
  const config = useRuntimeConfig()
  const { getToken } = useAuthToken()

  const apiConfig = {
    baseURL: config.public.apiBaseUrl || 'http://localhost:5000/api/v1',
  }

  // Create authenticated request headers
  const getAuthHeaders = () => {
    const token = getToken()
    return token
      ? { Authorization: `Bearer ${token}` }
      : {}
  }

  // Base request function with error handling
  const request = async (endpoint, options = {}) => {
    try {
      const response = await $fetch(
        `${apiConfig.baseURL}${endpoint}`,
        {
          headers: {
            'Content-Type': 'application/json',
            ...getAuthHeaders(),
            ...(options.headers || {})
          },
          ...options
        }
      )
      return response
    } catch (error) {
      // Handle different error types
      throw createError({
        statusCode: error.status || 500,
        statusMessage: error.message || 'API Error'
      })
    }
  }

  return {
    get: (endpoint, params) =>
      request(endpoint, { method: 'GET', params }),

    post: (endpoint, body) =>
      request(endpoint, { method: 'POST', body }),

    put: (endpoint, body) =>
      request(endpoint, { method: 'PUT', body }),

    delete: (endpoint) =>
      request(endpoint, { method: 'DELETE' }),
  }
}
```

### 2. Token Management (`composables/auth/useAuthToken.js`)

```javascript
export function useAuthToken() {
  const tokenCookie = useCookie('auth-token', {
    default: () => null,
    secure: true,
    sameSite: 'strict'
  })

  const refreshTokenCookie = useCookie('refresh-token', {
    default: () => null,
    secure: true,
    sameSite: 'strict'
  })

  const setToken = (token, refreshToken) => {
    tokenCookie.value = token
    refreshTokenCookie.value = refreshToken
  }

  const getToken = () => {
    return tokenCookie.value
  }

  const getRefreshToken = () => {
    return refreshTokenCookie.value
  }

  const removeToken = () => {
    tokenCookie.value = null
    refreshTokenCookie.value = null
  }

  return {
    setToken,
    getToken,
    getRefreshToken,
    removeToken
  }
}
```

### 3. Authentication Composable (`composables/auth/useAuth.js`)

```javascript
export function useAuth() {
  const api = useApi()
  const { setToken, removeToken, getToken, getRefreshToken } = useAuthToken()

  const user = ref(null)
  const isAuthenticated = computed(() => !!user.value)
  const isLoading = ref(false)

  // Login
  const login = async (credentials) => {
    isLoading.value = true
    try {
      const response = await api.post('/auth/login', credentials)

      if (response.success && response.data) {
        setToken(response.data.token, response.data.refreshToken)
        user.value = response.data.user
        await navigateTo('/dashboard')
        return { success: true }
      }

      return { success: false, error: response.error?.message }
    } catch (error) {
      return {
        success: false,
        error: error.message || 'Login failed'
      }
    } finally {
      isLoading.value = false
    }
  }

  // Logout
  const logout = async () => {
    try {
      const refreshToken = getRefreshToken()
      if (refreshToken) {
        await api.post('/auth/revoke', { refreshToken })
      }
    } catch (error) {
      console.warn('Logout API call failed:', error)
    } finally {
      removeToken()
      user.value = null
      await navigateTo('/login')
    }
  }

  // Refresh token
  const refreshToken = async () => {
    try {
      const currentToken = getToken()
      const currentRefreshToken = getRefreshToken()

      if (!currentRefreshToken) return false

      const response = await api.post('/auth/refresh', {
        token: currentToken, // expired token
        refreshToken: currentRefreshToken
      })

      if (response.success && response.data) {
        setToken(response.data.token, response.data.refreshToken)
        user.value = response.data.user
        return true
      }

      return false
    } catch (error) {
      console.error('Token refresh failed:', error)
      await logout()
      return false
    }
  }

  // Initialize auth state
  const initialize = async () => {
    const token = getToken()
    if (!token) return

    try {
      const response = await api.get('/users/me')
      if (response.success && response.data) {
        user.value = response.data
      }
    } catch (error) {
      if (error.statusCode === 401) {
        // Try to refresh token
        const refreshed = await refreshToken()
        if (refreshed) {
          await initialize() // Retry with new token
        }
      }
    }
  }

  return {
    user: readonly(user),
    isAuthenticated,
    isLoading: readonly(isLoading),
    login,
    logout,
    refreshToken,
    initialize
  }
}
```

## Resource Composables

### Groups Composable (`composables/resources/useGroups.js`)

```javascript
export function useGroups() {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groups = ref([])
  const currentGroup = ref(null)
  const isLoading = ref(false)

  // Get user's groups
  const fetchGroups = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/groups')
      if (response.success && response.data) {
        groups.value = response.data
      }
    } catch (error) {
      showError('Failed to load groups')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Create group
  const createGroup = async (groupData) => {
    isLoading.value = true
    try {
      const response = await api.post('/groups', groupData)
      if (response.success && response.data) {
        groups.value.push(response.data)
        showSuccess('Group created successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to create group')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Get group details
  const fetchGroup = async (groupId) => {
    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupId}`)
      if (response.success && response.data) {
        currentGroup.value = response.data
        return response.data
      }
    } catch (error) {
      showError('Failed to load group details')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Update group
  const updateGroup = async (groupId, updates) => {
    isLoading.value = true
    try {
      const response = await api.put(`/groups/${groupId}`, updates)
      if (response.success && response.data) {
        const index = groups.value.findIndex(g => g.id === groupId)
        if (index !== -1) {
          groups.value[index] = response.data
        }
        if (currentGroup.value?.id === groupId) {
          currentGroup.value = response.data
        }
        showSuccess('Group updated successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to update group')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Delete group
  const deleteGroup = async (groupId) => {
    isLoading.value = true
    try {
      await api.delete(`/groups/${groupId}`)
      groups.value = groups.value.filter(g => g.id !== groupId)
      if (currentGroup.value?.id === groupId) {
        currentGroup.value = null
      }
      showSuccess('Group deleted successfully')
    } catch (error) {
      showError('Failed to delete group')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Get group members
  const fetchGroupMembers = async (groupId) => {
    try {
      const response = await api.get(`/groups/${groupId}/members`)
      if (response.success && response.data) {
        return response.data
      }
    } catch (error) {
      showError('Failed to load group members')
      throw error
    }
  }

  // Add group member
  const addGroupMember = async (groupId, memberData) => {
    try {
      const response = await api.post(`/groups/${groupId}/members`, memberData)
      if (response.success && response.data) {
        showSuccess('Member added successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to add member')
      throw error
    }
  }

  // Remove group member
  const removeGroupMember = async (groupId, userId) => {
    try {
      await api.delete(`/groups/${groupId}/members/${userId}`)
      showSuccess('Member removed successfully')
    } catch (error) {
      showError('Failed to remove member')
      throw error
    }
  }

  return {
    groups: readonly(groups),
    currentGroup: readonly(currentGroup),
    isLoading: readonly(isLoading),
    fetchGroups,
    createGroup,
    fetchGroup,
    updateGroup,
    deleteGroup,
    fetchGroupMembers,
    addGroupMember,
    removeGroupMember
  }
}
```

### Expenses Composable (`composables/resources/useExpenses.js`)

```javascript
export function useExpenses(groupId) {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const expenses = ref([])
  const currentExpense = ref(null)
  const pagination = ref({
    page: 1,
    limit: 20,
    total: 0,
    totalPages: 0,
    hasNext: false,
    hasPrev: false
  })
  const isLoading = ref(false)

  // Fetch expenses with filtering and pagination
  const fetchExpenses = async (filters = {}) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const params = {
        page: filters.page || 1,
        limit: filters.limit || 20,
        ...(filters.startDate && { startDate: filters.startDate }),
        ...(filters.endDate && { endDate: filters.endDate }),
        ...(filters.category && { category: filters.category }),
        ...(filters.userId && { userId: filters.userId })
      }

      const response = await api.get(
        `/groups/${groupIdRef.value}/expenses`,
        params
      )

      if (response.success && response.data) {
        expenses.value = response.data
        pagination.value = response.pagination || {
          page: 1, limit: 20, total: 0, totalPages: 0,
          hasNext: false, hasPrev: false
        }
      }
    } catch (error) {
      showError('Failed to load expenses')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Get single expense
  const fetchExpense = async (expenseId) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/expenses/${expenseId}`)
      if (response.success && response.data) {
        currentExpense.value = response.data
        return response.data
      }
    } catch (error) {
      showError('Failed to load expense')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Create expense
  const createExpense = async (expenseData) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post(
        `/groups/${groupIdRef.value}/expenses`,
        expenseData
      )

      if (response.success && response.data) {
        expenses.value.unshift(response.data)
        showSuccess('Expense created successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to create expense')
      throw error
    }
  }

  // Update expense
  const updateExpense = async (expenseId, updates) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.put(
        `/groups/${groupIdRef.value}/expenses/${expenseId}`,
        updates
      )

      if (response.success && response.data) {
        const index = expenses.value.findIndex(e => e.id === expenseId)
        if (index !== -1) {
          expenses.value[index] = response.data
        }
        if (currentExpense.value?.id === expenseId) {
          currentExpense.value = response.data
        }
        showSuccess('Expense updated successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to update expense')
      throw error
    }
  }

  // Delete expense
  const deleteExpense = async (expenseId) => {
    if (!groupIdRef.value) return

    try {
      await api.delete(`/groups/${groupIdRef.value}/expenses/${expenseId}`)
      expenses.value = expenses.value.filter(e => e.id !== expenseId)
      if (currentExpense.value?.id === expenseId) {
        currentExpense.value = null
      }
      showSuccess('Expense deleted successfully')
    } catch (error) {
      showError('Failed to delete expense')
      throw error
    }
  }

  return {
    expenses: readonly(expenses),
    currentExpense: readonly(currentExpense),
    pagination: readonly(pagination),
    isLoading: readonly(isLoading),
    fetchExpenses,
    fetchExpense,
    createExpense,
    updateExpense,
    deleteExpense
  }
}
```

### Settlements Composable (`composables/resources/useSettlements.js`)

```javascript
export function useSettlements(groupId) {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const settlements = ref([])
  const currentSettlement = ref(null)
  const pagination = ref({
    page: 1,
    limit: 20,
    total: 0,
    totalPages: 0,
    hasNext: false,
    hasPrev: false
  })
  const isLoading = ref(false)

  // Fetch settlements with filtering and pagination
  const fetchSettlements = async (filters = {}) => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const params = {
        page: filters.page || 1,
        limit: filters.limit || 20,
        ...(filters.startDate && { startDate: filters.startDate }),
        ...(filters.endDate && { endDate: filters.endDate })
      }

      const response = await api.get(
        `/groups/${groupIdRef.value}/settlements`,
        params
      )

      if (response.success && response.data) {
        settlements.value = response.data
        pagination.value = response.pagination || {
          page: 1, limit: 20, total: 0, totalPages: 0,
          hasNext: false, hasPrev: false
        }
      }
    } catch (error) {
      showError('Failed to load settlements')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Create settlement
  const createSettlement = async (settlementData) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post(
        `/groups/${groupIdRef.value}/settlements`,
        settlementData
      )

      if (response.success && response.data) {
        settlements.value.unshift(response.data)
        showSuccess('Settlement created successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to create settlement')
      throw error
    }
  }

  // Update settlement
  const updateSettlement = async (settlementId, updates) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.put(
        `/groups/${groupIdRef.value}/settlements/${settlementId}`,
        updates
      )

      if (response.success && response.data) {
        const index = settlements.value.findIndex(s => s.id === settlementId)
        if (index !== -1) {
          settlements.value[index] = response.data
        }
        if (currentSettlement.value?.id === settlementId) {
          currentSettlement.value = response.data
        }
        showSuccess('Settlement updated successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to update settlement')
      throw error
    }
  }

  // Delete settlement
  const deleteSettlement = async (settlementId) => {
    if (!groupIdRef.value) return

    try {
      await api.delete(`/groups/${groupIdRef.value}/settlements/${settlementId}`)
      settlements.value = settlements.value.filter(s => s.id !== settlementId)
      if (currentSettlement.value?.id === settlementId) {
        currentSettlement.value = null
      }
      showSuccess('Settlement deleted successfully')
    } catch (error) {
      showError('Failed to delete settlement')
      throw error
    }
  }

  // Confirm settlement
  const confirmSettlement = async (settlementId) => {
    if (!groupIdRef.value) return

    try {
      const response = await api.post(
        `/groups/${groupIdRef.value}/settlements/${settlementId}/confirm`
      )

      if (response.success && response.data) {
        const index = settlements.value.findIndex(s => s.id === settlementId)
        if (index !== -1) {
          settlements.value[index] = response.data
        }
        if (currentSettlement.value?.id === settlementId) {
          currentSettlement.value = response.data
        }
        showSuccess('Settlement confirmed successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to confirm settlement')
      throw error
    }
  }

  return {
    settlements: readonly(settlements),
    currentSettlement: readonly(currentSettlement),
    pagination: readonly(pagination),
    isLoading: readonly(isLoading),
    fetchSettlements,
    createSettlement,
    updateSettlement,
    deleteSettlement,
    confirmSettlement
  }
}
```

### Balances Composable (`composables/resources/useBalances.js`)

```javascript
export function useBalances(groupId) {
  const api = useApi()
  const { showError } = useNotifications()

  const groupIdRef = toRef(groupId)
  const balances = ref([])
  const balanceSummary = ref(null)
  const isLoading = ref(false)

  // Get current balances
  const fetchBalances = async () => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/balances`)
      if (response.success && response.data) {
        balances.value = response.data
      }
    } catch (error) {
      showError('Failed to load balances')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Get balance summary with suggestions
  const fetchBalanceSummary = async () => {
    if (!groupIdRef.value) return

    isLoading.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/balances/summary`)
      if (response.success && response.data) {
        balanceSummary.value = response.data
      }
    } catch (error) {
      showError('Failed to load balance summary')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  return {
    balances: readonly(balances),
    balanceSummary: readonly(balanceSummary),
    isLoading: readonly(isLoading),
    fetchBalances,
    fetchBalanceSummary
  }
}
```

### Users Composable (`composables/resources/useUsers.js`)

```javascript
export function useUsers() {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const users = ref([])
  const currentUser = ref(null)
  const userImports = ref([])
  const isLoading = ref(false)

  // Get all users (admin only)
  const fetchUsers = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/users')
      if (response.success && response.data) {
        users.value = response.data
      }
    } catch (error) {
      showError('Failed to load users')
      throw error
    } finally {
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
    } catch (error) {
      showError('Failed to load user profile')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // Update current user profile
  const updateCurrentUser = async (userData) => {
    try {
      const response = await api.put('/users/me', userData)
      if (response.success && response.data) {
        currentUser.value = response.data
        showSuccess('Profile updated successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to update profile')
      throw error
    }
  }

  // Change password
  const changePassword = async (passwordData) => {
    try {
      await api.put('/users/me/password', passwordData)
      showSuccess('Password changed successfully')
    } catch (error) {
      showError('Failed to change password')
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
    } catch (error) {
      showError('Failed to load imports')
      throw error
    }
  }

  // Create user (admin only)
  const createUser = async (userData) => {
    try {
      const response = await api.post('/users', userData)
      if (response.success && response.data) {
        users.value.push(response.data)
        showSuccess('User created successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to create user')
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
    } catch (error) {
      showError('Failed to load user')
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
        showSuccess('User updated successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to update user')
      throw error
    }
  }

  // Delete user (admin only)
  const deleteUser = async (userId) => {
    try {
      await api.delete(`/users/${userId}`)
      users.value = users.value.filter(u => u.id !== userId)
      showSuccess('User deleted successfully')
    } catch (error) {
      showError('Failed to delete user')
      throw error
    }
  }

  return {
    users: readonly(users),
    currentUser: readonly(currentUser),
    userImports: readonly(userImports),
    isLoading: readonly(isLoading),
    fetchUsers,
    fetchCurrentUser,
    updateCurrentUser,
    changePassword,
    fetchUserImports,
    createUser,
    fetchUser,
    updateUser,
    deleteUser
  }
}
```

### Categories Composable (`composables/resources/useCategories.js`)

```javascript
export function useCategories() {
  const api = useApi()
  const { showError } = useNotifications()

  const categories = ref([])
  const isLoading = ref(false)

  // Get available categories
  const fetchCategories = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/categories')
      if (response.success && response.data) {
        categories.value = response.data
      }
    } catch (error) {
      showError('Failed to load categories')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  return {
    categories: readonly(categories),
    isLoading: readonly(isLoading),
    fetchCategories
  }
}
```

### Payment Modes Composable (`composables/resources/usePaymentModes.js`)

```javascript
export function usePaymentModes() {
  const api = useApi()
  const { showError } = useNotifications()

  const paymentModes = ref([])
  const isLoading = ref(false)

  // Get available payment modes
  const fetchPaymentModes = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/payment-modes')
      if (response.success && response.data) {
        paymentModes.value = response.data
      }
    } catch (error) {
      showError('Failed to load payment modes')
      throw error
    } finally {
      isLoading.value = false
    }
  }

  return {
    paymentModes: readonly(paymentModes),
    isLoading: readonly(isLoading),
    fetchPaymentModes
  }
}
```

### Import/Export Composable (`composables/resources/useImportExport.js`)

```javascript
export function useImportExport(groupId) {
  const api = useApi()
  const { showError, showSuccess } = useNotifications()

  const groupIdRef = toRef(groupId)
  const isImporting = ref(false)
  const isExporting = ref(false)

  // Import data backup
  const importData = async (file, importTypeId = 1) => {
    if (!groupIdRef.value) return

    isImporting.value = true
    try {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('importTypeId', importTypeId)

      const response = await api.post(
        `/groups/${groupIdRef.value}/import`,
        formData,
        {
          headers: {
            'Content-Type': 'multipart/form-data'
          }
        }
      )

      if (response.success && response.data) {
        showSuccess('Data imported successfully')
        return response.data
      }
    } catch (error) {
      showError('Failed to import data')
      throw error
    } finally {
      isImporting.value = false
    }
  }

  // Export to CSV
  const exportToCsv = async () => {
    if (!groupIdRef.value) return

    isExporting.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/export/csv`)

      // Handle file download
      const blob = new Blob([response], { type: 'text/csv' })
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `splitduo-export-${groupIdRef.value}.csv`
      document.body.appendChild(a)
      a.click()
      window.URL.revokeObjectURL(url)
      document.body.removeChild(a)

      showSuccess('Data exported successfully')
    } catch (error) {
      showError('Failed to export data')
      throw error
    } finally {
      isExporting.value = false
    }
  }

  // Export to Cospend format
  const exportToCospend = async () => {
    if (!groupIdRef.value) return

    isExporting.value = true
    try {
      const response = await api.get(`/groups/${groupIdRef.value}/export/cospend`)

      // Handle file download
      const blob = new Blob([response], { type: 'application/json' })
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `cospend-export-${groupIdRef.value}.json`
      document.body.appendChild(a)
      a.click()
      window.URL.revokeObjectURL(url)
      document.body.removeChild(a)

      showSuccess('Cospend export successful')
    } catch (error) {
      showError('Failed to export to Cospend format')
      throw error
    } finally {
      isExporting.value = false
    }
  }

  return {
    isImporting: readonly(isImporting),
    isExporting: readonly(isExporting),
    importData,
    exportToCsv,
    exportToCospend
  }
}
```

## Utility Composables

### Notifications (`composables/utils/useNotifications.js`)

```javascript
export function useNotifications() {
  const toast = useToast()

  const showSuccess = (message, title = 'Success') => {
    toast.add({
      title,
      description: message,
      color: 'green'
    })
  }

  const showError = (message, title = 'Error') => {
    toast.add({
      title,
      description: message,
      color: 'red'
    })
  }

  const showWarning = (message, title = 'Warning') => {
    toast.add({
      title,
      description: message,
      color: 'yellow'
    })
  }

  const showInfo = (message, title = 'Info') => {
    toast.add({
      title,
      description: message,
      color: 'blue'
    })
  }

  return {
    showSuccess,
    showError,
    showWarning,
    showInfo
  }
}
```

### Error Handling (`composables/utils/useErrorHandling.js`)

```javascript
export function useErrorHandling() {
  const { showError } = useNotifications()

  const handleApiError = (error, defaultMessage = 'An error occurred') => {
    const message = error?.data?.error?.message ||
                   error?.message ||
                   defaultMessage

    showError(message)
  }

  const handleValidationErrors = (errors) => {
    if (Array.isArray(errors)) {
      errors.forEach(error => {
        showError(`${error.field}: ${error.message}`)
      })
    }
  }

  const handleAuthError = (error) => {
    if (error.statusCode === 401) {
      showError('Authentication required. Please log in.')
      navigateTo('/login')
    } else if (error.statusCode === 403) {
      showError('Access denied. You do not have permission.')
    } else {
      handleApiError(error)
    }
  }

  return {
    handleApiError,
    handleValidationErrors,
    handleAuthError
  }
}
```

### Pagination (`composables/utils/usePagination.js`)

```javascript
export function usePagination() {
  const createPaginatedList = (initialData = []) => {
    const items = ref(initialData)
    const pagination = ref({
      page: 1,
      limit: 20,
      total: 0,
      totalPages: 0,
      hasNext: false,
      hasPrev: false
    })
    const isLoading = ref(false)

    const nextPage = () => {
      if (pagination.value.hasNext) {
        pagination.value.page++
      }
    }

    const prevPage = () => {
      if (pagination.value.hasPrev) {
        pagination.value.page--
      }
    }

    const goToPage = (page) => {
      if (page >= 1 && page <= pagination.value.totalPages) {
        pagination.value.page = page
      }
    }

    const setLimit = (limit) => {
      pagination.value.limit = limit
      pagination.value.page = 1 // Reset to first page when changing limit
    }

    // Computed properties for easy access
    const currentPageItems = computed(() => items.value)
    const hasItems = computed(() => items.value.length > 0)
    const isEmpty = computed(() => !hasItems.value && !isLoading.value)
    const totalPages = computed(() => pagination.value.totalPages)
    const currentPage = computed(() => pagination.value.page)

    return {
      items,
      pagination: readonly(pagination),
      isLoading: readonly(isLoading),
      currentPageItems,
      hasItems,
      isEmpty,
      totalPages,
      currentPage,
      nextPage,
      prevPage,
      goToPage,
      setLimit
    }
  }

  return {
    createPaginatedList
  }
}
```

## Usage Examples

### Basic Component Usage

```vue
<script setup>
// Groups page
const { groups, fetchGroups, createGroup, isLoading } = useGroups()

// Fetch groups on mount
await fetchGroups()

// Create new group
const handleCreateGroup = async (groupData) => {
  try {
    await createGroup(groupData)
    // Group automatically added to reactive groups array
  } catch (error) {
    // Error automatically shown via composable
  }
}
</script>

<template>
  <div>
    <div v-if="isLoading">Loading...</div>

    <div v-else>
      <div v-for="group in groups" :key="group.id">
        {{ group.name }}
      </div>
    </div>

    <UButton @click="handleCreateGroup({ name: 'New Group' })">
      Create Group
    </UButton>
  </div>
</template>
```

### Advanced Component with Filters

```vue
<script setup>
// Expenses page with filtering
const route = useRoute()
const groupId = route.params.groupId

const {
  expenses,
  fetchExpenses,
  createExpense,
  pagination,
  isLoading
} = useExpenses(groupId)

// Reactive filters
const filters = ref({
  startDate: '',
  endDate: '',
  category: '',
  page: 1
})

// Watch filters and refetch
watchEffect(() => {
  fetchExpenses(filters.value)
})

// Handle pagination
const handlePageChange = (page) => {
  filters.value.page = page
}

// Handle filter changes
const handleFilterChange = () => {
  filters.value.page = 1 // Reset to first page when filters change
}
</script>

<template>
  <div>
    <!-- Filter controls -->
    <div class="filters mb-4">
      <UInput
        v-model="filters.startDate"
        type="date"
        placeholder="Start Date"
        @change="handleFilterChange"
      />
      <UInput
        v-model="filters.endDate"
        type="date"
        placeholder="End Date"
        @change="handleFilterChange"
      />
      <UInput
        v-model="filters.category"
        placeholder="Category"
        @input="handleFilterChange"
      />
    </div>

    <!-- Expenses list -->
    <div v-if="isLoading">Loading...</div>

    <div v-else-if="expenses.length === 0">
      No expenses found
    </div>

    <div v-else>
      <div v-for="expense in expenses" :key="expense.id" class="expense-item">
        <h3>{{ expense.title }}</h3>
        <p>${{ expense.amount }}</p>
        <p>{{ expense.expenseDate }}</p>
        <p>Paid by: {{ expense.paidByUser.firstName }} {{ expense.paidByUser.lastName }}</p>
      </div>

      <!-- Pagination -->
      <div class="pagination mt-4">
        <UPagination
          :total="pagination.total"
          :page="pagination.page"
          :limit="pagination.limit"
          @update:page="handlePageChange"
        />
      </div>
    </div>
  </div>
</template>
```

### Authentication Plugin Setup

```javascript
// plugins/auth.client.js
export default defineNuxtPlugin(async () => {
  const { initialize } = useAuth()

  // Initialize auth state on app start
  await initialize()
})
```

### Middleware for Protected Routes

```javascript
// middleware/auth.js
export default defineNuxtRouteMiddleware((to) => {
  const { isAuthenticated } = useAuth()

  if (!isAuthenticated.value) {
    return navigateTo('/login')
  }
})
```

### Group-specific Middleware

```javascript
// middleware/group-member.js
export default defineNuxtRouteMiddleware(async (to) => {
  const { isAuthenticated } = useAuth()
  const { fetchGroup } = useGroups()

  if (!isAuthenticated.value) {
    return navigateTo('/login')
  }

  try {
    const groupId = to.params.groupId
    await fetchGroup(groupId)
  } catch (error) {
    if (error.statusCode === 403) {
      throw createError({
        statusCode: 403,
        statusMessage: 'You are not a member of this group'
      })
    }
    throw error
  }
})
```

## Configuration Setup

### Runtime Config (`nuxt.config.js`)

```javascript
export default defineNuxtConfig({
  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL || 'http://localhost:5000/api/v1'
    }
  },

  modules: [
    '@nuxt/ui'
  ],

  // Auto-import composables
  imports: {
    dirs: [
      'composables/**'
    ]
  }
})
```

### Environment Variables

```env
# .env
NUXT_PUBLIC_API_BASE_URL=http://localhost:5000/api/v1

# Production
NUXT_PUBLIC_API_BASE_URL=https://api.splitduo.app/v1
```

## Best Practices

### 1. **Error Handling**

- Always use try-catch blocks in composables
- Provide user-friendly error messages
- Handle different error types appropriately (401, 403, 404, etc.)

### 2. **Loading States**

- Always provide loading indicators
- Use readonly refs to prevent external mutation
- Handle loading states in UI components

### 3. **Data Management**

- Use reactive refs for data storage
- Implement proper cleanup when needed
- Cache data appropriately to avoid unnecessary API calls

### 4. **Security**

- Store tokens securely using httpOnly cookies when possible
- Implement automatic token refresh
- Handle authentication errors gracefully

### 5. **Performance**

- Use pagination for large data sets
- Implement debouncing for search/filter inputs
- Consider using lazy loading for non-critical data

### 6. **Testing**

- Write unit tests for composables
- Mock API calls in tests
- Test error scenarios

## Implementation Checklist

### Phase 1: Core Infrastructure

- [ ] Set up base API configuration (`composables/api/base.js`)
- [ ] Implement token management (`composables/auth/useAuthToken.js`)
- [ ] Create authentication composable (`composables/auth/useAuth.js`)
- [ ] Set up notifications utility (`composables/utils/useNotifications.js`)

### Phase 2: Resource Composables

- [ ] Implement groups composable (`composables/resources/useGroups.js`)
- [ ] Implement users composable (`composables/resources/useUsers.js`)
- [ ] Implement expenses composable (`composables/resources/useExpenses.js`)
- [ ] Implement settlements composable (`composables/resources/useSettlements.js`)
- [ ] Implement balances composable (`composables/resources/useBalances.js`)

### Phase 3: Supporting Features

- [ ] Implement categories composable (`composables/resources/useCategories.js`)
- [ ] Implement payment modes composable (`composables/resources/usePaymentModes.js`)
- [ ] Implement import/export composable (`composables/resources/useImportExport.js`)
- [ ] Set up pagination utilities (`composables/utils/usePagination.js`)

### Phase 4: Integration

- [ ] Create authentication plugin
- [ ] Set up route middleware
- [ ] Configure runtime config
- [ ] Update existing components to use composables

### Phase 5: Testing & Optimization

- [ ] Write unit tests for composables
- [ ] Test error handling scenarios
- [ ] Optimize performance and caching
- [ ] Add TypeScript support (optional)

## Conclusion

This composables structure provides a robust, maintainable, and scalable foundation for the SplitDuo frontend application. It follows Vue 3 and Nuxt 3 best practices while ensuring type safety, proper error handling, and excellent developer experience.

The modular approach allows for easy testing, reusability across components, and future enhancements without major refactoring. Each composable encapsulates its own logic while providing clean interfaces for components to consume.
