import { createEnum } from './enumUtils'
import type { EnumResult, EnumDefinition } from './enumUtils'

const userRoleDefinition = {
  BASE_USER: {
    value: 1,
    label: 'User',
  },
  SYSTEM_ADMIN: {
    value: 2,
    label: 'System Admin',
  },
} as const satisfies EnumDefinition

export type UserRoleDefinition = typeof userRoleDefinition

export const UserRole: EnumResult<UserRoleDefinition> = createEnum(userRoleDefinition)

// Legacy exports for backward compatibility
export const UserRoleLabels: Record<number, string> = UserRole.Labels
export const getUserRoleOptions = (): Array<{ label: string, value: number }> => UserRole.getSelectOptions()
