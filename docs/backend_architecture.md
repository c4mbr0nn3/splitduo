# SplitDuo Backend Architecture

## Overview

This document outlines the technical decisions and architectural patterns used in the SplitDuo backend solution.

## Project Structure

### Vertical Slice Architecture

The `SplitDuo.Api` project follows a **Vertical Slice Architecture** pattern, organizing code by features rather than technical concerns:

```bash
SplitDuo.Api/
├── Features/
│   ├── Authentication/
│   │   ├── Controllers/
│   │   └── Dto/
│   ├── Users/
│   │   ├── Controllers/
│   │   └── Dto/
│   ├── Groups/
│   │   ├── Controllers/
│   │   └── Dto/
│   ├── Expenses/
│   │   ├── Controllers/
│   │   └── Dto/
│   ├── Settlements/
│   │   ├── Controllers/
│   │   └── Dto/
│   ├── Import/
│   │   ├── Controllers/
│   │   └── Dto/
│   ├── Export/
│   │   ├── Controllers/
│   │   └── Dto/
│   └── Common/
│       └── Dto/
```

**Benefits:**

- Related functionality grouped together
- Easy to locate and modify feature-specific code
- Clear separation of concerns
- Reduced coupling between features

## Service Layer Pattern

### Architecture Flow

```bash
Controllers → Services → Unit of Work → AppDbContext → Database
```

**Controllers:**

- Handle HTTP requests/responses
- Validate input via DTOs
- Delegate business logic to Services
- Handle Result pattern responses from Services
- Manage Unit of Work SaveChanges operations
- Return standardized API responses

**Services:**

- Contain business logic and rules
- Use Result pattern for error handling
- Queue database operations via Unit of Work
- Handle data validation and transformation
- Do NOT perform save operations (handled by Controllers)

**Unit of Work:**

- Manages database transactions
- Centralizes SaveChanges operations
- Enables chaining multiple operations before committing
- Ensures transactional consistency across operations

**Example Service Structure:**

```csharp
public class UsersService
{
    private readonly IUnitOfWork _unitOfWork;

    public UsersService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequestDto request)
    {
        // Business logic and validation
        if (validation fails)
            return Result<UserDto>.Failure("Validation error");

        // Queue database operations (no save)
        _unitOfWork.Users.Add(user);
        _unitOfWork.Notifications.Add(notification);

        return Result<UserDto>.Success(userDto);
    }
}

// Controller handles save
public async Task<ActionResult> CreateUser(CreateUserRequestDto request)
{
    var result = await _usersService.CreateUserAsync(request);
    if (result.IsFailure) return BadRequest(result.Error);

    await _unitOfWork.SaveChangesAsync();
    return Ok(result.Value);
}
```

## Data Access

### Entity Framework Core

- **ORM**: Entity Framework Core with PostgreSQL
- **Context**: Single `AppDbContext` for all data operations
- **Interceptors**: Audit and soft delete functionality
- **Migrations**: Code-first approach

### Database Context Features

- Soft delete interceptor for safe data removal
- Audit interceptor for tracking changes
- Centralized configuration in `OnModelCreating`

## Application Initialization

### DataSeederService

- **Type**: IHostedService (background service)
- **Purpose**: Create initial users after migrations
- **Configuration**: Uses AppOptions pattern
- **Execution**: Runs once at application startup

**Configuration Keys:**

- `App:InitialUserEmail`
- `App:InitialUserFirstName`
- `App:InitialUserLastName`
- `App:InitialUserPassword`

### Migration Strategy

- Clean migrations without hardcoded data
- Initial data seeded post-migration via DataSeederService
- Separation of schema and data concerns

## Authentication & Authorization

### JWT-Based Authentication

- **Token Type**: JSON Web Tokens (JWT)
- **Flow**: Login → JWT token → Protected endpoints
- **No Registration**: Users created via admin endpoints only
- **Password Management**: Individual password changes allowed

### Security Considerations

- Password hashing using ASP.NET Core Identity
- Bearer token authentication
- Secure configuration via AppOptions

## Configuration Management

### Options Pattern

- **AppOptions**: Application-level configuration
- **DatabaseOptions**: Database connection settings
- **JwtOptions**: JWT authentication settings
- **Setup Classes**: Dedicated option configuration classes

## Error Handling

### Global Exception Handler

The application uses a **Global Exception Handler** for centralized error management:

**Implementation:**

- **Handler Class**: `GlobalExceptionHandler` implementing `IExceptionHandler`
- **Registration**: Configured via `AddExceptionHandler<GlobalExceptionHandler>()` in DI container
- **Middleware Integration**: Uses ASP.NET Core's built-in exception handling middleware

**Features:**

- **Centralized Logging** - All unhandled exceptions logged automatically with full context
- **Consistent Responses** - Standardized Problem Details format for all error responses
- **Status Code Mapping** - Automatic HTTP status code assignment based on exception type
- **Clean Controllers** - No try-catch boilerplate required in endpoint methods

**Error Response Format:**

```json
{
  "type": "ExceptionTypeName",
  "title": "An unhandled exception occurred",
  "status": 500,
  "detail": "Exception message"
}
```

**Benefits:**

- **Reduced Boilerplate** - Controllers focus on business logic, not error handling
- **Consistent Logging** - Single point for exception logging prevents duplicate entries
- **Uniform API Responses** - All errors follow same response structure
- **Maintainability** - Error handling logic centralized and easily modified

## Technical Decisions Summary

1. **Vertical Slice Architecture** - Feature-based organization over technical layers
2. **Service Layer with Result Pattern** - Business logic separation with explicit error handling
3. **Unit of Work Pattern** - Centralized transaction and save operations management
4. **Single DbContext** - Centralized data access point
5. **No Registration Endpoint** - Admin-managed user creation only
6. **Hosted Service Seeding** - Post-migration data initialization
7. **Options Pattern** - Strongly-typed configuration management
8. **JWT Authentication** - Stateless authentication for API access
9. **Global Exception Handler** - Centralized error handling and logging across all endpoints
10. **Email Notification System** - Outbox pattern with background processing
11. **Logging System** - Serilog with environment-specific sinks and database storage

## Email Notification System

### Outbox Pattern Architecture

The application uses an **Outbox Pattern** for reliable email notifications:

**Flow:**

```bash
Controllers → Services → Notifications Table → Background Job → Email Provider
```

**Components:**

- **Notifications Table** - Persistent queue for outgoing notifications
- **Services** - Queue notifications during business operations
- **Background Job** - Processes notifications asynchronously
- **Email Provider** - Sends actual emails

### Database Design

- **Notifications Table** - Stores pending/sent notifications
- **Transactional Consistency** - Notifications queued in same transaction as business data
- **Status Tracking** - Pending, Processing, Sent, Failed states
- **Retry Logic** - Failed notifications can be retried
- **Cleanup Policy** - Sent notifications pruned after 30 days to maintain lightweight database

### Notification Types & Prioritization

#### Phase 1 (Essential)

- **User created** - Welcome email with login credentials
- **Expense added** - Notify other group members about new expense
- **Settlement created** - Important for payment coordination between users

#### Phase 2 (Important)

- **Settlement confirmed** - Confirms payment completion
- **Added to group** - Welcome notification for group membership
- **Password changed** - Security notification

#### Phase 3 (Nice to have)

- **Expense updated** - Keep users informed of changes
- **Expense deleted** - Transparency about expense removal
- **Balance reminder** - Periodic outstanding balance alerts

#### Phase 4 (Future)

- **Removed from group** - Group membership changes
- **Group deleted** - Group lifecycle notifications
- **User deleted** - Account deletion confirmation

### Implementation Benefits

- **Reliability** - Notifications survive application restarts
- **Performance** - Non-blocking business operations
- **Monitoring** - Track notification delivery status
- **Scalability** - Background processing prevents bottlenecks

## Logging System

### Serilog Configuration

The application uses **Serilog** for structured logging with environment-specific configurations:

**Development Environment:**

- **Console Sink** - Logs output to console only
- **Log Level** - Debug/Information for detailed development feedback

**Production Environment:**

- **Console Sink** - Standard output for container logging
- **PostgreSQL Sink** - Persistent log storage in dedicated database table
- **Log Level** - Warning/Error to reduce noise

### Database Logging

- **Storage**: Dedicated logs table in PostgreSQL database
- **Sink**: Serilog PostgreSQL sink for structured log data
- **Retention**: Logs automatically pruned after 30 days
- **Structure**: JSON-formatted log entries with metadata

### Log Management

- **Cleanup Policy** - Automated 30-day log retention
- **Performance** - Asynchronous logging to prevent blocking
- **Monitoring** - Centralized log storage for production troubleshooting
- **Configuration** - Environment-based sink selection via Serilog configuration
