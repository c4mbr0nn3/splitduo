export const UserRole = {
  BASE_USER: 1,
  SYSTEM_ADMIN: 2,
}

export const UserRoleLabels = {
  [UserRole.BASE_USER]: 'User',
  [UserRole.SYSTEM_ADMIN]: 'System Admin',
}

export const getUserRoleOptions = () => [
  { label: UserRoleLabels[UserRole.BASE_USER], value: UserRole.BASE_USER },
  { label: UserRoleLabels[UserRole.SYSTEM_ADMIN], value: UserRole.SYSTEM_ADMIN },
]
