import { createEnum } from './enumUtils'

export const UserRole = createEnum({
  BASE_USER: {
    value: 1,
    label: 'User',
  },
  SYSTEM_ADMIN: {
    value: 2,
    label: 'System Admin',
  },
})

// Legacy exports for backward compatibility
export const UserRoleLabels = UserRole.Labels
export const getUserRoleOptions = () => UserRole.getSelectOptions()
