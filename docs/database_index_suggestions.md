# Database Index Suggestions for SplitDuo

## Overview

Based on the database schema and API structure, here are recommended indexes to optimize query performance for the SplitDuo expense splitting application.

## Existing Indexes

The schema already includes these composite unique indexes:

- `group_members(group_id, user_id)` - Ensures unique membership
- `expense_splits(expense_id, user_id)` - Ensures unique splits per expense

## Recommended Additional Indexes

### Users Table

```sql
-- Unique constraint on email for authentication and user uniqueness
CREATE UNIQUE INDEX idx_users_email ON users(email) WHERE deleted_at IS NULL;

-- Fast lookup by GUID for frontend operations
CREATE INDEX idx_users_guid ON users(guid) WHERE deleted_at IS NULL;

-- Query active users efficiently
CREATE INDEX idx_users_deleted_at ON users(deleted_at);
```

### Refresh Tokens Table

```sql
-- Primary lookup for token validation (critical for auth performance)
CREATE UNIQUE INDEX idx_refresh_tokens_token_hash ON refresh_tokens(token_hash);

-- User token management for revocation scenarios
CREATE INDEX idx_refresh_tokens_user_revoked ON refresh_tokens(user_id, revoked_at);

-- Cleanup expired tokens (background maintenance)
CREATE INDEX idx_refresh_tokens_expires_at ON refresh_tokens(expires_at);

-- JWT correlation for token validation
CREATE INDEX idx_refresh_tokens_jwt_id ON refresh_tokens(jwt_id);

-- Active token lookup for security operations
CREATE INDEX idx_refresh_tokens_active ON refresh_tokens(user_id)
WHERE revoked_at IS NULL AND expires_at > EXTRACT(EPOCH FROM NOW());
```

### Groups Table

```sql
-- Fast lookup by GUID for frontend operations
CREATE INDEX idx_groups_guid ON groups(guid) WHERE deleted_at IS NULL;

-- Fast lookup by creator for user's groups
CREATE INDEX idx_groups_created_by ON groups(created_by_user_id) WHERE deleted_at IS NULL;

-- Query active groups efficiently
CREATE INDEX idx_groups_deleted_at ON groups(deleted_at);
```

### Group Members Table

```sql
-- Fast lookup of user's groups
CREATE INDEX idx_group_members_user_id ON group_members(user_id) WHERE deleted_at IS NULL;

-- Fast lookup of group members
CREATE INDEX idx_group_members_group_id ON group_members(group_id) WHERE deleted_at IS NULL;

-- Query active memberships efficiently
CREATE INDEX idx_group_members_deleted_at ON group_members(deleted_at);
```

### Expenses Table

```sql
-- Fast lookup by GUID for frontend operations
CREATE INDEX idx_expenses_guid ON expenses(guid) WHERE deleted_at IS NULL;

-- Core query: expenses by group (most common operation)
CREATE INDEX idx_expenses_group_date ON expenses(group_id, expense_date DESC) WHERE deleted_at IS NULL;

-- Query expenses by payer
CREATE INDEX idx_expenses_paid_by ON expenses(paid_by_user_id, expense_date DESC) WHERE deleted_at IS NULL;

-- Category filtering within groups
CREATE INDEX idx_expenses_category ON expenses(group_id, category, expense_date DESC) WHERE deleted_at IS NULL;

-- Date range queries for exports and reports
CREATE INDEX idx_expenses_date_range ON expenses(expense_date) WHERE deleted_at IS NULL;

-- Query active expenses efficiently
CREATE INDEX idx_expenses_deleted_at ON expenses(deleted_at);
```

### Expense Splits Table

```sql
-- Fast lookup by user for balance calculations
CREATE INDEX idx_expense_splits_user_id ON expense_splits(user_id);

-- Fast lookup by expense for split details
CREATE INDEX idx_expense_splits_expense_id ON expense_splits(expense_id);
```

### Settlements Table

```sql
-- Fast lookup by GUID for frontend operations
CREATE INDEX idx_settlements_guid ON settlements(guid) WHERE deleted_at IS NULL;

-- Core query: settlements by group
CREATE INDEX idx_settlements_group_date ON settlements(group_id, settlement_date DESC) WHERE deleted_at IS NULL;

-- Query settlements by participants
CREATE INDEX idx_settlements_from_user ON settlements(from_user_id, settlement_date DESC) WHERE deleted_at IS NULL;
CREATE INDEX idx_settlements_to_user ON settlements(to_user_id, settlement_date DESC) WHERE deleted_at IS NULL;

-- Query active settlements efficiently
CREATE INDEX idx_settlements_deleted_at ON settlements(deleted_at);
```

### Imports Table

```sql
-- Fast lookup by GUID for frontend operations
CREATE INDEX idx_imports_guid ON imports(guid);

-- Query imports by user
CREATE INDEX idx_imports_user ON imports(user_id, import_date DESC);

-- Query imports by group
CREATE INDEX idx_imports_group ON imports(group_id, import_date DESC);

-- Query import status
CREATE INDEX idx_imports_status ON imports(status, import_date DESC);
```

## Performance Optimization Strategies

### Soft Delete Optimization

Since the application uses soft deletes (`deleted_at` field), most indexes include `WHERE deleted_at IS NULL` to:

- Exclude deleted records from index
- Improve query performance
- Reduce index size

### Balance Calculation Optimization

For frequent balance calculations, consider:

```sql
-- Composite index for balance queries
CREATE INDEX idx_balance_calculation ON expense_splits(user_id, expense_id);

-- Include expense date for date-filtered balance calculations
CREATE INDEX idx_balance_with_date ON expense_splits(user_id)
INCLUDE (split_amount)
WHERE EXISTS (
  SELECT 1 FROM expenses e
  WHERE e.id = expense_splits.expense_id
  AND e.deleted_at IS NULL
);
```

### Query-Specific Optimizations

```sql
-- For group dashboard queries (most recent expenses)
CREATE INDEX idx_group_recent_expenses ON expenses(group_id, created_at DESC)
WHERE deleted_at IS NULL;

-- For user activity queries
CREATE INDEX idx_user_activity ON expenses(paid_by_user_id, created_at DESC)
WHERE deleted_at IS NULL;

-- For export operations with date ranges
CREATE INDEX idx_export_date_range ON expenses(group_id, expense_date, created_at)
WHERE deleted_at IS NULL;
```

## Monitoring and Maintenance

### Index Usage Monitoring

```sql
-- Monitor index usage (PostgreSQL specific)
SELECT
  schemaname,
  tablename,
  indexname,
  idx_tup_read,
  idx_tup_fetch
FROM pg_stat_user_indexes
ORDER BY idx_tup_read DESC;
```

### Query Performance Analysis

```sql
-- Enable query logging for slow queries
SET log_min_duration_statement = 1000; -- Log queries taking >1s

-- Use EXPLAIN ANALYZE for query optimization
EXPLAIN ANALYZE SELECT * FROM expenses
WHERE group_id = $1 AND deleted_at IS NULL
ORDER BY expense_date DESC;
```

### Security Performance Considerations

**RefreshToken Table Specific**:

```sql
-- Monitor authentication query performance
EXPLAIN ANALYZE
SELECT user_id, jwt_id, expires_at, revoked_at
FROM refresh_tokens
WHERE token_hash = $1;

-- Monitor token cleanup performance
EXPLAIN ANALYZE
DELETE FROM refresh_tokens
WHERE expires_at < EXTRACT(EPOCH FROM NOW() - INTERVAL '30 days');

-- Monitor user token revocation performance
EXPLAIN ANALYZE
UPDATE refresh_tokens
SET revoked_at = EXTRACT(EPOCH FROM NOW()), revoked_reason = 'Security breach'
WHERE user_id = $1 AND revoked_at IS NULL;
```

**Authentication Performance Targets**:

- Token validation queries: < 10ms
- Token cleanup operations: < 100ms
- Bulk token revocation: < 500ms

## Implementation Priority

1. **High Priority** (implement immediately):

   - **RefreshToken indexes** (critical for authentication performance and security)
   - GUID lookups for all tables
   - Group-based queries (expenses, settlements)
   - Soft delete indexes

2. **Medium Priority** (implement after initial deployment):

   - Date range filtering indexes
   - Category filtering indexes
   - Balance calculation optimizations

3. **Low Priority** (implement based on usage patterns):
   - User activity indexes
   - Import status tracking indexes
   - Advanced reporting indexes

## Notes

- Monitor actual query patterns in production to validate index effectiveness
- Consider partitioning strategies for large datasets (expenses by date)
- Review and adjust indexes based on user behavior and performance metrics
- Use PostgreSQL's built-in statistics to identify missing indexes
