import { describe, it, expect } from 'vitest'
import { UserRole, UserRoleLabels, getUserRoleOptions } from './userRoles'

describe('userRoles', () => {
  describe('UserRole enum', () => {
    it('maps BASE_USER to 1', () => {
      expect(UserRole.BASE_USER).toBe(1)
    })

    it('maps SYSTEM_ADMIN to 2', () => {
      expect(UserRole.SYSTEM_ADMIN).toBe(2)
    })
  })

  describe('UserRoleLabels', () => {
    it('maps each role value to its display label', () => {
      expect(UserRoleLabels[1]).toBe('User')
      expect(UserRoleLabels[2]).toBe('System Admin')
    })
  })

  describe('getUserRoleOptions', () => {
    it('returns select options with label and value for each role', () => {
      expect(getUserRoleOptions()).toEqual([
        { label: 'User', value: 1 },
        { label: 'System Admin', value: 2 },
      ])
    })
  })
})
