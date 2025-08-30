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

## Implementation Priority

1. **High Priority** (implement immediately):

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
