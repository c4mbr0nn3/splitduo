# SplitDuo REST API Structure

## Overview

The SplitDuo REST API follows RESTful conventions and uses JSON for request/response payloads. All endpoints require authentication except for login/register.

## Base Configuration

- **Base URL**: `https://splitduo.app/api/v1`
- **Content-Type**: `application/json`
- **Authentication**: Bearer token (JWT)
- **Response Format**: JSON

## API Endpoints

### Authentication

| Method | Endpoint         | Description       |
| ------ | ---------------- | ----------------- |
| POST   | `/auth/login`    | User login        |
| POST   | `/auth/register` | User registration |
| POST   | `/auth/refresh`  | Refresh JWT token |
| POST   | `/auth/logout`   | User logout       |

### Users

| Method | Endpoint    | Description                 |
| ------ | ----------- | --------------------------- |
| GET    | `/users/me` | Get current user profile    |
| PUT    | `/users/me` | Update current user profile |
| DELETE | `/users/me` | Delete current user account |

### Groups

| Method | Endpoint                             | Description              |
| ------ | ------------------------------------ | ------------------------ |
| GET    | `/groups`                            | Get user's groups        |
| POST   | `/groups`                            | Create new group         |
| GET    | `/groups/{groupId}`                  | Get group details        |
| PUT    | `/groups/{groupId}`                  | Update group             |
| DELETE | `/groups/{groupId}`                  | Delete group             |
| GET    | `/groups/{groupId}/members`          | Get group members        |
| POST   | `/groups/{groupId}/members`          | Add member to group      |
| DELETE | `/groups/{groupId}/members/{userId}` | Remove member from group |

### Expenses

| Method | Endpoint                                 | Description         |
| ------ | ---------------------------------------- | ------------------- |
| GET    | `/groups/{groupId}/expenses`             | Get group expenses  |
| POST   | `/groups/{groupId}/expenses`             | Create new expense  |
| GET    | `/groups/{groupId}/expenses/{expenseId}` | Get expense details |
| PUT    | `/groups/{groupId}/expenses/{expenseId}` | Update expense      |
| DELETE | `/groups/{groupId}/expenses/{expenseId}` | Delete expense      |

### Settlements

| Method | Endpoint                                               | Description           |
| ------ | ------------------------------------------------------ | --------------------- |
| GET    | `/groups/{groupId}/settlements`                        | Get group settlements |
| POST   | `/groups/{groupId}/settlements`                        | Create new settlement |
| PUT    | `/groups/{groupId}/settlements/{settlementId}`         | Update settlement     |
| DELETE | `/groups/{groupId}/settlements/{settlementId}`         | Delete settlement     |
| POST   | `/groups/{groupId}/settlements/{settlementId}/confirm` | Confirm settlement    |

### Balances

| Method | Endpoint                             | Description          |
| ------ | ------------------------------------ | -------------------- |
| GET    | `/groups/{groupId}/balances`         | Get current balances |
| GET    | `/groups/{groupId}/balances/summary` | Get balance summary  |

### Data Import/Export

| Method | Endpoint                           | Description           |
| ------ | ---------------------------------- | --------------------- |
| POST   | `/groups/{groupId}/import/cospend` | Import Cospend backup |
| GET    | `/groups/{groupId}/export/csv`     | Export to CSV         |
| GET    | `/groups/{groupId}/export/cospend` | Export Cospend format |
| GET    | `/imports/{importId}/status`       | Get import status     |

## Error Handling

### Standard HTTP Status Codes

- **200**: Success
- **201**: Created
- **400**: Bad Request (validation errors)
- **401**: Unauthorized (authentication required)
- **403**: Forbidden (insufficient permissions)
- **404**: Not Found
- **409**: Conflict (duplicate resource)
- **422**: Unprocessable Entity (business logic errors)
- **500**: Internal Server Error

### Error Response Format

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid request data",
    "details": [
      {
        "field": "email",
        "message": "Email is required"
      }
    ]
  }
}
```

## Pagination

For list endpoints that return multiple items:

### Query Parameters

- `page`: Page number (default: 1)
- `limit`: Items per page (default: 20, max: 100)
- `sort`: Sort field (default: created_at)
- `order`: Sort order (asc/desc, default: desc)

### Response Format

```json
{
  "data": [...],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 150,
    "totalPages": 8,
    "hasNext": true,
    "hasPrev": false
  }
}
```

## Filtering

### Common Query Parameters

- `startDate`: Filter by start date (ISO 8601 format sent by frontend)
- `endDate`: Filter by end date (ISO 8601 format sent by frontend)
- `category`: Filter by expense category
- `userId`: Filter by user (where applicable)

### Date vs Timestamp Fields

- **Date-only fields** (expense_date, settlement_date, import_date): Sent and returned as ISO 8601 dates (e.g., "2024-01-15")
- **Timestamp fields** (created_at, updated_at, deleted_at, joined_at): Returned as unix timestamps in seconds

### Example

```bash
GET /api/v1/groups/123/expenses?startDate=2024-01-01&endDate=2024-01-31&category=food
```

## Rate Limiting

- **Limit**: 100 requests per minute per user
- **Headers**:
  - `X-RateLimit-Limit`: Request limit
  - `X-RateLimit-Remaining`: Remaining requests
  - `X-RateLimit-Reset`: Reset unix timestamp

## Versioning

- **Current Version**: v1
- **Versioning Strategy**: URL path versioning (`/api/v1/`)
- **Deprecation Policy**: 6 months notice for breaking changes
