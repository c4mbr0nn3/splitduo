# SplitDuo API DTO Definitions

## Authentication DTOs

### LoginRequestDto

```json
{
  "email": "string (required, email format)",
  "password": "string (required, min 8 chars)"
}
```

### RegisterRequestDto

```json
{
  "email": "string (required, email format)",
  "password": "string (required, min 8 chars)",
  "firstName": "string (required, max 100 chars)",
  "lastName": "string (optional, max 100 chars)"
}
```

### AuthResponseDto

```json
{
  "token": "string (JWT token)",
  "refreshToken": "string",
  "expiresAt": "number (unix timestamp in seconds)",
  "user": {
    "id": "string (GUID)",
    "email": "string",
    "firstName": "string",
    "lastName": "string"
  }
}
```

### RefreshTokenRequestDto

```json
{
  "refreshToken": "string (required)"
}
```

## User DTOs

### UserDto

```json
{
  "id": "string (GUID)",
  "email": "string",
  "firstName": "string",
  "lastName": "string",
  "createdAt": "number (unix timestamp in seconds)",
  "updatedAt": "number (unix timestamp in seconds)"
}
```

### UpdateUserRequestDto

```json
{
  "firstName": "string (optional, max 100 chars)",
  "lastName": "string (optional, max 100 chars)",
  "email": "string (optional, email format)"
}
```

## Group DTOs

### GroupDto

```json
{
  "id": "string (GUID)",
  "name": "string",
  "description": "string",
  "createdByUserId": "string (GUID)",
  "memberCount": "number",
  "createdAt": "number (unix timestamp in seconds)",
  "updatedAt": "number (unix timestamp in seconds)"
}
```

### CreateGroupRequestDto

```json
{
  "name": "string (required, max 200 chars)",
  "description": "string (optional)"
}
```

### UpdateGroupRequestDto

```json
{
  "name": "string (optional, max 200 chars)",
  "description": "string (optional)"
}
```

### GroupMemberDto

```json
{
  "id": "string (GUID)",
  "groupId": "string (GUID)",
  "userId": "string (GUID)",
  "user": {
    "id": "string (GUID)",
    "email": "string",
    "firstName": "string",
    "lastName": "string"
  },
  "role": "string (member|admin)",
  "joinedAt": "number (unix timestamp in seconds)"
}
```

### AddGroupMemberRequestDto

```json
{
  "userEmail": "string (required, email format)",
  "role": "string (optional, default: member)"
}
```

## Expense DTOs

### ExpenseDto

```json
{
  "id": "string (GUID)",
  "groupId": "string (GUID)",
  "title": "string",
  "description": "string",
  "amount": "number (decimal)",
  "paidByUserId": "string (GUID)",
  "paidByUser": {
    "id": "string (GUID)",
    "firstName": "string",
    "lastName": "string"
  },
  "expenseDate": "number (unix timestamp in seconds)",
  "category": "string",
  "splits": [
    {
      "id": "string (GUID)",
      "userId": "string (GUID)",
      "user": {
        "id": "string (GUID)",
        "firstName": "string",
        "lastName": "string"
      },
      "splitAmount": "number (decimal)",
      "splitPercentage": "number (decimal, optional)"
    }
  ],
  "createdAt": "number (unix timestamp in seconds)",
  "updatedAt": "number (unix timestamp in seconds)"
}
```

### CreateExpenseRequestDto

```json
{
  "title": "string (required, max 255 chars)",
  "description": "string (optional)",
  "amount": "number (required, decimal, positive)",
  "paidByUserId": "string (required, GUID)",
  "expenseDate": "string (required, ISO 8601 date)",
  "category": "string (optional, max 100 chars)",
  "splits": [
    {
      "userId": "string (required, GUID)",
      "splitAmount": "number (optional, decimal)",
      "splitPercentage": "number (optional, decimal 0-100)"
    }
  ]
}
```

### UpdateExpenseRequestDto

```json
{
  "title": "string (optional, max 255 chars)",
  "description": "string (optional)",
  "amount": "number (optional, decimal, positive)",
  "paidByUserId": "string (optional, GUID)",
  "expenseDate": "string (optional, ISO 8601 date)",
  "category": "string (optional, max 100 chars)",
  "splits": [
    {
      "userId": "string (required, GUID)",
      "splitAmount": "number (optional, decimal)",
      "splitPercentage": "number (optional, decimal 0-100)"
    }
  ]
}
```

## Settlement DTOs

### SettlementDto

```json
{
  "id": "string (GUID)",
  "groupId": "string (GUID)",
  "fromUserId": "string (GUID)",
  "fromUser": {
    "id": "string (GUID)",
    "firstName": "string",
    "lastName": "string"
  },
  "toUserId": "string (GUID)",
  "toUser": {
    "id": "string (GUID)",
    "firstName": "string",
    "lastName": "string"
  },
  "amount": "number (decimal)",
  "settlementDate": "number (unix timestamp in seconds)",
  "description": "string",
  "isConfirmed": "boolean",
  "createdAt": "number (unix timestamp in seconds)",
  "updatedAt": "number (unix timestamp in seconds)"
}
```

### CreateSettlementRequestDto

```json
{
  "fromUserId": "string (required, GUID)",
  "toUserId": "string (required, GUID)",
  "amount": "number (required, decimal, positive)",
  "settlementDate": "string (required, ISO 8601 date)",
  "description": "string (optional)"
}
```

### UpdateSettlementRequestDto

```json
{
  "amount": "number (optional, decimal, positive)",
  "settlementDate": "string (optional, ISO 8601 date)",
  "description": "string (optional)"
}
```

## Balance DTOs

### BalanceDto

```json
{
  "userId": "string (GUID)",
  "user": {
    "id": "string (GUID)",
    "firstName": "string",
    "lastName": "string"
  },
  "balance": "number (decimal)",
  "totalPaid": "number (decimal)",
  "totalOwed": "number (decimal)"
}
```

### BalanceSummaryDto

```json
{
  "groupId": "string (GUID)",
  "balances": [
    {
      "userId": "string (GUID)",
      "user": {
        "id": "string (GUID)",
        "firstName": "string",
        "lastName": "string"
      },
      "balance": "number (decimal)"
    }
  ],
  "suggestions": [
    {
      "fromUserId": "string (GUID)",
      "toUserId": "string (GUID)",
      "amount": "number (decimal)",
      "description": "string"
    }
  ]
}
```

## Import/Export DTOs

### CospendImportRequestDto

```json
{
  "file": "file (multipart/form-data)",
  "groupId": "string (required, GUID)"
}
```

### ImportStatusDto

```json
{
  "id": "string (GUID)",
  "filename": "string",
  "status": "string (pending|completed|failed)",
  "recordsImported": "number",
  "errorDetails": "string",
  "importDate": "number (unix timestamp in seconds)"
}
```

### ExportRequestDto

```json
{
  "format": "string (csv|cospend)",
  "startDate": "string (optional, ISO 8601 date)",
  "endDate": "string (optional, ISO 8601 date)",
  "includeSettlements": "boolean (optional, default: true)"
}
```

## Common Response Wrappers

### Success Response

```json
{
  "success": true,
  "data": "object|array (response data)",
  "message": "string (optional success message)"
}
```

### Error Response

```json
{
  "success": false,
  "error": {
    "code": "string (error code)",
    "message": "string (user-friendly message)",
    "details": "array (validation errors, optional)"
  }
}
```

### Paginated Response

```json
{
  "success": true,
  "data": "array (response items)",
  "pagination": {
    "page": "number",
    "limit": "number",
    "total": "number",
    "totalPages": "number",
    "hasNext": "boolean",
    "hasPrev": "boolean"
  }
}
```

## Validation Rules

### General Rules

- All GUID fields must be valid UUID v7 format
- All decimal amounts must be positive and have max 2 decimal places
- All required fields must be present and non-empty
- Email fields must be valid email format
- Input dates must be ISO 8601 format, response timestamps are unix timestamps in seconds

### Business Rules

- Expense splits must sum to the total expense amount
- Users can only access groups they are members of
- Only group admins can add/remove members
- Settlement amounts must be positive
- Users cannot settle with themselves

## Headers

### Required Headers

```bash
Authorization: Bearer {jwt_token}
Content-Type: application/json
```

### Optional Headers

```bash
X-Request-ID: string (for request tracing)
Accept-Language: string (for localization)
```

## Examples

### Create Expense Example

```bash
POST /api/v1/groups/550e8400-e29b-41d4-a716-446655440000/expenses
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

{
  "title": "Grocery Shopping",
  "description": "Weekly groceries from Target",
  "amount": 85.50,
  "paidByUserId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "expenseDate": "2024-01-15",
  "category": "groceries",
  "splits": [
    {
      "userId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
      "splitAmount": 42.75
    },
    {
      "userId": "6ba7b811-9dad-11d1-80b4-00c04fd430c8",
      "splitAmount": 42.75
    }
  ]
}
```

### Get Balances Example

```bash
GET /api/v1/groups/550e8400-e29b-41d4-a716-446655440000/balances
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

Response:
{
  "success": true,
  "data": [
    {
      "userId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
      "user": {
        "id": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
        "firstName": "John",
        "lastName": "Doe"
      },
      "balance": -25.50,
      "totalPaid": 150.00,
      "totalOwed": 175.50
    }
  ]
}
```
