/**
 * Domain types for the SplitDuo frontend.
 *
 * Source of truth: `app/types/api.d.ts` (generated from `docs/api/splitduoapi-v1.yaml`
 * via `pnpm gen:api`). This file re-exports generated schemas with friendlier names,
 * extracts the API envelope types, and adds the alias-mode unions the spec can't
 * express. Do NOT edit `api.d.ts` by hand — regenerate it.
 *
 * The OpenAPI spec marks all response fields as optional (no `required` arrays on
 * response DTOs), so generated types have everything as `field?:`. We use `Required<T>`
 * to make known-required fields non-optional for the entity types the frontend
 * actually uses. This is a frontend-side narrowing — the backend always sends these
 * fields. If the backend ever omits a field we've marked required, the runtime
 * behavior is unchanged (TS doesn't enforce at runtime); the type just claims it's
 * there.
 *
 * Hand-write exceptions (not in the OpenAPI spec):
 * - `BalanceSummaryDto` / `BalanceSuggestionDto` — normal-mode balance summary. The
 *   spec only surfaces the alias-mode variants (`AliasBalanceSummaryDto`,
 *   `AliasSettlementSuggestionDto`) because .NET's OpenAPI generator emits only one
 *   response schema per status code, and the alias variant won the dual
 *   `[ProducesResponseType]` race. These match the backend C# DTOs in
 *   `sd-backend/SplitDuo.Api/Features/Expenses/Dto/BalanceDto.cs`.
 * - `ImportAnalysis` / `KeyValue` — the parsed shape of the `analysisResults` JSON
 *   string inside `ImportStatusDto`. The backend stores it as a string; the frontend
 *   parses it at runtime. These match `sd-backend/SplitDuo.Core/Dto/Imports/`.
 */

import type { components } from './api'

// ─── Helper ──────────────────────────────────────────────────────────────────

/**
 * Make specified keys of T required (non-optional). Inverse of `Partial<Pick<...>>`.
 * Use for generated DTO fields the backend always sends.
 */
type WithRequired<T, K extends keyof T> = T & Required<Pick<T, K>>

// ─── API envelope types ───────────────────────────────────────────────────────

/** Error object inside an API response envelope. */
export type ApiError = components['schemas']['ApiErrorDto']

/** Non-paginated response envelope: `{ success, data?, message?, error? }`. */
export interface ApiEnvelope<T> {
  success: boolean
  data?: T | null
  message?: string | null
  error?: ApiError | null
}

/** Paginated response envelope: `{ success, data: T[], pagination, message?, error? }`. */
export interface PaginatedEnvelope<T> {
  success: boolean
  data: T[]
  pagination: Pagination
  message?: string | null
  error?: ApiError | null
}

/** Pagination metadata returned by paginated endpoints. */
export type Pagination = WithRequired<components['schemas']['PaginationDto'], 'page' | 'limit' | 'total' | 'totalPages' | 'hasNext' | 'hasPrev'>

// ─── Entity types (re-exported from generated, with required narrowing) ──────

export type User = WithRequired<components['schemas']['UserDto'], 'id' | 'email' | 'firstName' | 'globalRoleId' | 'createdAt' | 'updatedAt' | 'twoFactorEnabled' | 'settings' | 'hasAvatar'>
export type UserBasicInfo = WithRequired<components['schemas']['UserBasicInfoDto'], 'id' | 'firstName' | 'hasAvatar'>
export type UserInfo = WithRequired<components['schemas']['UserInfoDto'], 'id' | 'email' | 'firstName' | 'hasAvatar'>
export type UserSettings = WithRequired<components['schemas']['UserSettingsDto'], 'theme' | 'uiLanguage'>
export type UserStats = WithRequired<components['schemas']['UserStatsDto'], 'totalGroups' | 'youOwe' | 'youreOwed'>

export type Group = WithRequired<components['schemas']['GroupDto'], 'id' | 'name' | 'createdByUserId' | 'memberCount' | 'createdAt' | 'updatedAt' | 'netBalance' | 'useAliases' | 'aliasSetupFinalized'>
export type GroupMember = WithRequired<components['schemas']['GroupMemberDto'], 'groupId' | 'userId' | 'user' | 'role' | 'joinedAt'>
export type GroupStats = WithRequired<components['schemas']['GroupStatsDto'], 'expenseCount' | 'totalAmount' | 'balances' | 'categoryBreakdown' | 'monthlyBreakdown'>
export type CategoryStat = WithRequired<components['schemas']['CategoryStatDto'], 'categoryId' | 'categoryName' | 'amount' | 'count'>
export type MonthlyStat = WithRequired<components['schemas']['MonthlyStatDto'], 'year' | 'month' | 'amount' | 'count'>

export type Expense = WithRequired<components['schemas']['ExpenseDto'], 'id' | 'groupId' | 'title' | 'amount' | 'paidByUserId' | 'paidByUser' | 'expenseDate' | 'categoryId' | 'paymentModeId' | 'splits' | 'attachmentCount' | 'createdAt' | 'updatedAt'>
export type ExpenseSplit = WithRequired<components['schemas']['ExpenseSplitDto'], 'id' | 'userId' | 'user' | 'splitAmount'>
export type ExpenseAliasSplit = WithRequired<components['schemas']['ExpenseAliasSplitDto'], 'id' | 'aliasId' | 'aliasName' | 'splitAmount'>
export type ExpenseAttachment = WithRequired<components['schemas']['ExpenseAttachmentDto'], 'id' | 'expenseId' | 'filenameOriginal' | 'mimeType' | 'sizeBytes' | 'createdAt' | 'updatedAt'>

export type Alias = WithRequired<components['schemas']['AliasDto'], 'id' | 'name' | 'groupId' | 'isSingleton' | 'createdAt' | 'updatedAt'>

export type Category = WithRequired<components['schemas']['CategoryDto'], 'id' | 'name'>
export type PaymentMode = WithRequired<components['schemas']['PaymentModeDto'], 'id' | 'name'>

export type Invitation = WithRequired<components['schemas']['InvitationDto'], 'id' | 'email' | 'invitedBy' | 'groupName' | 'invitedAt' | 'expiresAt'>
export type PendingUser = WithRequired<components['schemas']['PendingUserDto'], 'email' | 'groups'>
export type PendingUserGroup = WithRequired<components['schemas']['PendingUserGroupDto'], 'id' | 'name' | 'invitedAt' | 'expiresAt'>
export type SendInvitationResponse = WithRequired<components['schemas']['SendInvitationResponseDto'], 'type'>
export type ValidateInvitationResponse = WithRequired<components['schemas']['ValidateInvitationResponseDto'], 'email' | 'groupName' | 'expiresAt'>

export type ImportStatus = WithRequired<components['schemas']['ImportStatusDto'], 'id' | 'fileName' | 'fileHash' | 'importStatusId' | 'importTypeId' | 'recordsCount' | 'errorDetails' | 'importDate' | 'createdAt' | 'updatedAt'>

export type AuthResponse = WithRequired<components['schemas']['AuthResponseDto'], 'token' | 'refreshToken' | 'expiresAt' | 'requiresTwoFactor' | 'user'>
export type TwoFactorSetup = WithRequired<components['schemas']['TwoFactorSetupDto'], 'secret' | 'qrCodeUri' | 'backupCodes'>
export type ParsedReceipt = WithRequired<components['schemas']['ParsedReceiptDto'], 'title' | 'amount' | 'expenseDate'>
export type AiStatus = WithRequired<components['schemas']['AiStatusDto'], 'enabled'>

// ─── Balance types (alias-mode union) ────────────────────────────────────────

export type NormalBalance = WithRequired<components['schemas']['BalanceDto'], 'userId' | 'user' | 'balance' | 'totalPaid' | 'totalOwed'>
export type AliasBalance = WithRequired<components['schemas']['AliasBalanceDto'], 'aliasId' | 'aliasName' | 'balance' | 'totalPaid' | 'totalOwed' | 'members' | 'isSingleton'>

/** Balance entry — normal or alias mode. Narrow via `'aliasId' in b` or `isAliasMode`. */
export type Balance = NormalBalance | AliasBalance

// Hand-write: BalanceSummaryDto not in spec (only AliasBalanceSummaryDto surfaced).
// Matches sd-backend/SplitDuo.Api/Features/Expenses/Dto/BalanceDto.cs.
export interface BalanceSuggestion {
  fromUserId: string
  toUserId: string
  amount: number
  description: string
}

export interface BalanceSummary {
  groupId: string
  balances: NormalBalance[]
  suggestions: BalanceSuggestion[]
}

export type AliasSettlementSuggestion = WithRequired<components['schemas']['AliasSettlementSuggestionDto'], 'fromAliasId' | 'toAliasId' | 'fromAliasName' | 'toAliasName' | 'amount' | 'description'>
export type AliasBalanceSummary = WithRequired<components['schemas']['AliasBalanceSummaryDto'], 'groupId' | 'balances' | 'suggestions'>

/** Balance summary — normal or alias mode. Narrow via `'aliasId' in b.balances[0]`. */
export type BalanceSummaryUnion = BalanceSummary | AliasBalanceSummary

// ─── Import analysis (hand-write: parsed from JSON string at runtime) ────────

// Hand-write: ImportAnalysisDto/KeyValueDto not in spec (stored as JSON string in
// ImportStatusDto.analysisResults). Matches sd-backend/SplitDuo.Core/Dto/Imports/.
export interface KeyValue {
  key: string
  value: string
}

export interface ImportAnalysis {
  fileHash: string
  members: KeyValue[]
  categories: KeyValue[]
  paymentModes: KeyValue[]
  aliases: KeyValue[]
}

// ─── Request DTOs (re-exported from generated) ───────────────────────────────

export type LoginRequest = components['schemas']['LoginRequestDto']
export type RefreshTokenRequest = components['schemas']['RefreshTokenRequestDto']
export type VerifyTwoFactorLogin = components['schemas']['VerifyTwoFactorLoginDto']
export type VerifyTwoFactorSetup = components['schemas']['VerifyTwoFactorSetupDto']
export type DisableTwoFactor = components['schemas']['DisableTwoFactorDto']
export type ForgotPasswordRequest = components['schemas']['ForgotPasswordRequestDto']
export type ResetPasswordRequest = components['schemas']['ResetPasswordRequestDto']
export type RevokeTokenRequest = components['schemas']['RevokeTokenRequestDto']

export type CreateUserRequest = components['schemas']['AcceptInvitationRequestDto']
export type UpdateUserRequest = components['schemas']['UpdateUserRequestDto']
export type ChangePasswordRequest = components['schemas']['ChangePasswordRequestDto']
export type UpdateUserSettingsRequest = components['schemas']['UpdateUserSettingsRequestDto']
export type UpdateUserSettingsResponse = components['schemas']['UpdateUserSettingsResponseDto']

export type CreateGroupRequest = components['schemas']['CreateGroupRequestDto']
export type UpdateGroupRequest = components['schemas']['UpdateGroupRequestDto']
export type AddGroupMemberRequest = components['schemas']['AddGroupMemberRequestDto']
export type UpdateGroupMemberRoleRequest = components['schemas']['UpdateGroupMemberRoleRequestDto']

export type CreateExpenseRequest = components['schemas']['CreateExpenseRequestDto']
export type CreateExpenseSplit = components['schemas']['CreateExpenseSplitDto']
export type CreateExpenseAliasSplit = components['schemas']['CreateExpenseAliasSplitDto']
export type UpdateExpenseRequest = components['schemas']['UpdateExpenseRequestDto']
export type UpdateExpenseSplit = components['schemas']['UpdateExpenseSplitDto']

export type CreateAliasRequest = components['schemas']['CreateAliasRequestDto']
export type UpdateAliasRequest = components['schemas']['UpdateAliasRequestDto']
export type AssignAliasMemberRequest = components['schemas']['AssignAliasMemberRequestDto']

export type SendInvitationRequest = components['schemas']['SendInvitationRequestDto']
export type AcceptInvitationRequest = components['schemas']['AcceptInvitationRequestDto']

export type ImportMapping = components['schemas']['ImportMappingDto']

// ─── Split mode (frontend-only UI state — not persisted to backend) ──────────

/** Expense split mode: Amounts (user-entered amounts), Percentage (user-entered %). Equal is a one-click action, not a mode. */
export type SplitMode = 'amounts' | 'percentage'
