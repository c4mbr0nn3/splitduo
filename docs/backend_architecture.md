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
│       ├── Controllers/  # BaseApiController
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
- Use centralized Result handling via BaseApiController
- Manage Unit of Work SaveChanges operations
- Return standardized API responses with proper HTTP status codes

**Services:**

- Contain business logic and rules
- Use enhanced Result pattern with HTTP status codes for error handling
- Queue database operations via Unit of Work
- Handle data validation and transformation
- Do NOT perform save operations (handled by Controllers)
- Return appropriate HTTP status codes (401, 404, 409, etc.) via Result pattern

**Unit of Work:**

- Manages database transactions
- Centralizes SaveChanges operations
- Enables chaining multiple operations before committing
- Ensures transactional consistency across operations
- Exposes DbSets directly for entity access without repository layer
- Provides transaction management methods (BeginTransaction, Commit, Rollback)

**Example Service and Controller Structure:**

```csharp
// Service with enhanced Result pattern
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
        if (await EmailExistsAsync(request.Email))
            return Result<UserDto>.Conflict("Email already exists");

        if (!IsValidEmail(request.Email))
            return Result<UserDto>.UnprocessableEntity("Invalid email format");

        // Queue database operations (no save)
        _unitOfWork.Users.Add(user);
        // Note: Notification system would be implemented separately

        return Result<UserDto>.Success(userDto);
    }
}

// Controller with centralized Result handling
public class UsersController : BaseApiController
{
    private readonly UsersService _usersService;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<ActionResult<ApiResponseDto<UserDto>>> CreateUser(CreateUserRequestDto request)
    {
        var result = await _usersService.CreateUserAsync(request);

        if (result.IsSuccess)
            await _unitOfWork.SaveChangesAsync();

        return HandleResult(result, "User created successfully");
    }
}
```

## Enhanced Result Pattern & Centralized Response Handling

### Result Pattern with HTTP Status Codes

The application uses an enhanced Result pattern that includes HTTP status codes for precise error handling and response mapping.

**Location**: `SplitDuo.Core/Common/Result.cs`

**Features:**

- **HTTP Status Code Integration**: Each Result includes an `HttpStatusCode` property
- **Type Safety**: Uses `System.Net.HttpStatusCode` enum for consistent status mapping
- **Convenience Methods**: Static methods for common HTTP status codes
- **Backward Compatibility**: Default status codes maintain existing behavior

**Enhanced Result Structure:**

```csharp
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; private set; }
    public string Error { get; private set; } = "";
    public HttpStatusCode StatusCode { get; private set; } = HttpStatusCode.OK;

    // Convenience methods for common status codes
    public static Result<T> Success(T value, HttpStatusCode statusCode = HttpStatusCode.OK);
    public static Result<T> Failure(string error, HttpStatusCode statusCode = HttpStatusCode.BadRequest);
    public static Result<T> NotFound(string error = "Resource not found");
    public static Result<T> Unauthorized(string error = "Unauthorized access");
    public static Result<T> Forbidden(string error = "Forbidden access");
    public static Result<T> Conflict(string error = "Resource conflict");
    public static Result<T> UnprocessableEntity(string error = "Unprocessable entity");
    public static Result<T> InternalServerError(string error = "Internal server error");
}
```

**Service Usage Examples:**

```csharp
// Authentication scenarios
if (user == null)
    return Result<AuthResponseDto>.Unauthorized("Invalid email or password");

// Resource not found
if (expense == null)
    return Result<ExpenseDto>.NotFound("Expense not found");

// Business logic violations
if (await EmailExistsAsync(email))
    return Result<UserDto>.Conflict("Email already exists");

// Validation errors
if (!IsValidInput(request))
    return Result<UserDto>.UnprocessableEntity("Invalid input data");
```

### BaseApiController

**Location**: `SplitDuo.Api/Features/Common/Controllers/BaseApiController.cs`

The `BaseApiController` provides centralized Result handling, eliminating repetitive response mapping code in controllers.

**Features:**

- **Automatic Status Code Mapping**: Converts Result status codes to appropriate ActionResult responses
- **Consistent API Responses**: Maintains standardized response format using `ApiResponseDto<T>`
- **Error Code Generation**: Automatically generates error codes from HTTP status codes
- **Generic Support**: Handles both `Result<T>` and non-generic `Result` types

**Controller Base Implementation:**

```csharp
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponseDto<T>> HandleResult<T>(Result<T> result, string? successMessage = null)
    {
        if (result.IsSuccess)
        {
            var response = ApiResponseDto<T>.SuccessResponse(result.Value!, successMessage);
            return result.StatusCode switch
            {
                HttpStatusCode.OK => Ok(response),
                HttpStatusCode.Created => Created(string.Empty, response),
                HttpStatusCode.NoContent => NoContent(),
                _ => StatusCode((int)result.StatusCode, response)
            };
        }

        var errorResponse = ApiResponseDto<T>.ErrorResponse(
            GetErrorCodeFromStatus(result.StatusCode),
            result.Error
        );

        return result.StatusCode switch
        {
            HttpStatusCode.BadRequest => BadRequest(errorResponse),
            HttpStatusCode.Unauthorized => Unauthorized(errorResponse),
            HttpStatusCode.Forbidden => StatusCode(403, errorResponse),
            HttpStatusCode.NotFound => NotFound(errorResponse),
            HttpStatusCode.Conflict => Conflict(errorResponse),
            HttpStatusCode.UnprocessableEntity => StatusCode(422, errorResponse),
            HttpStatusCode.InternalServerError => StatusCode(500, errorResponse),
            _ => StatusCode((int)result.StatusCode, errorResponse)
        };
    }
}
```

**Controller Usage:**

```csharp
[Route("api/v1/auth")]
public class AuthController : BaseApiController
{
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authenticationService.LoginAsync(request);
        return HandleResult(result, "Login successful");
    }
}
```

**Benefits:**

- **Reduced Boilerplate**: Single line response handling: `return HandleResult(result, "Success message");`
- **Consistent Status Codes**: Automatic mapping from service-defined status codes to HTTP responses
- **Centralized Logic**: All response mapping logic in one place for easy maintenance
- **Type Safety**: Compile-time checking of status code handling
- **Standardized Responses**: Uniform API response structure across all endpoints

**HTTP Status Code Mapping:**

| Result Method              | HTTP Status              | Response Method   |
| -------------------------- | ------------------------ | ----------------- |
| `Success()`                | 200 OK                   | `Ok()`            |
| `Success()` with `Created` | 201 Created              | `Created()`       |
| `NotFound()`               | 404 Not Found            | `NotFound()`      |
| `Unauthorized()`           | 401 Unauthorized         | `Unauthorized()`  |
| `Forbidden()`              | 403 Forbidden            | `StatusCode(403)` |
| `Conflict()`               | 409 Conflict             | `Conflict()`      |
| `UnprocessableEntity()`    | 422 Unprocessable Entity | `StatusCode(422)` |
| `Failure()`                | 400 Bad Request          | `BadRequest()`    |

### Required Services for v1.0 MVP

The following services are needed to implement the core features outlined in the project specification:

#### Core Business Services

1. **AuthenticationService** _(implemented)_

   - User login/logout functionality
   - JWT token generation and validation
   - Password verification and token refresh handling
   - **Location**: `SplitDuo.Api/Services/AuthenticationService.cs`
   - **Features**: Result pattern integration, Unit of Work data access, ASP.NET Core Identity password hashing

2. **UsersService**

   - Create users (admin-only, no registration endpoint)
   - Update user profiles and change passwords
   - User management operations

3. **GroupsService**

   - Create and manage groups (primarily for couples)
   - Add/remove group members
   - Group settings management

4. **ExpensesService**

   - CRUD operations for expenses
   - Expense validation and business rules
   - Associate expenses with groups and users

5. **ExpenseSplitsService**

   - Calculate expense splits between users
   - Handle split logic (equal splits, custom amounts)
   - Generate split records for expenses

6. **BalancesService**

   - Calculate who owes what to whom
   - Generate balance summaries for groups
   - Real-time balance calculations

7. **SettlementsService**
   - Record payments between users
   - Update balances when settlements occur
   - Settlement history tracking

#### Data Management Services

8. **ImportService**

   - Import Cospend backup files
   - Data validation and transformation
   - Batch import operations with status tracking

9. **ExportService**
   - Export data to CSV format
   - Generate Cospend-compatible backup files
   - Data formatting and serialization

#### Infrastructure Services

10. **NotificationService**

    - Queue email notifications using outbox pattern
    - Handle notification types: user created, expense added, settlement created
    - Integration with background processing

11. **EmailService**
    - Send emails via email provider
    - Email templating and delivery status tracking

#### Existing Services

- **DataSeederService** - Initial user creation from configuration _(implemented)_
- **LogCleanupJob** - Background service for log maintenance _(implemented)_

#### Service Implementation Guidelines

- All services use `IUnitOfWork` for data access
- Services return `Result<T>` pattern for error handling
- Services contain business logic but do NOT call SaveChanges
- Controllers handle SaveChanges operations
- Services queue notifications for background processing
- Follow dependency injection patterns with scoped lifetimes

#### Implementation Details

**AuthenticationService Implementation:**

- **Architecture**: Service located in API project (`SplitDuo.Api/Services/`) to be accessible by controllers
- **Dependencies**: Uses `IUnitOfWork`, `IPasswordHasher<User>`, and `IOptions<JwtOptions>`
- **DTOs**: Utilizes existing DTOs from `SplitDuo.Api/Features/Authentication/Dto/`
- **Registration**: Registered in API layer DI container (`ApiProgramExtensions.cs`)
- **Password Hashing**: Leverages ASP.NET Core Identity `PasswordHasher<User>` for secure password operations
- **Token Management**: JWT generation with configurable expiration, refresh token support
- **Error Handling**: Consistent error responses using Result pattern
- **Integration**: DataSeederService updated to use injected `IPasswordHasher<User>` for consistency

## Data Access

### Entity Framework Core

- **ORM**: Entity Framework Core with PostgreSQL
- **Context**: Single `AppDbContext` for all data operations
- **Unit of Work**: `IUnitOfWork` implementation wrapping `AppDbContext`
- **Interceptors**: Audit and soft delete functionality
- **Migrations**: Code-first approach

### Database Context Features

- Soft delete interceptor for safe data removal
- Audit interceptor for tracking changes
- Centralized configuration in `OnModelCreating`

### Unit of Work Implementation

**Location**: `SplitDuo.Core/Persistence/UnitOfWork.cs`

**Features**:

- Interface and implementation in single file for tidiness
- Direct DbSet exposure: `Users`, `Groups`, `GroupMembers`, `Expenses`, `ExpenseSplits`, `Settlements`, `Imports`
- Transaction management: `BeginTransactionAsync`, `CommitTransactionAsync`, `RollbackTransactionAsync`
- Scoped lifetime registration in DI container
- Proper disposal pattern implementation

**Usage Pattern**:

```csharp
// In Services - queue operations
_unitOfWork.Users.Add(newUser);
_unitOfWork.Groups.Update(existingGroup);

// In Controllers - commit changes
await _unitOfWork.SaveChangesAsync();
```

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
2. **Enhanced Result Pattern with HTTP Status Codes** - Business logic separation with explicit error handling and precise HTTP status mapping
3. **BaseApiController with Centralized Response Handling** - Eliminates response mapping boilerplate and ensures consistent API responses
4. **Unit of Work Pattern** - Centralized transaction and save operations management
5. **Single DbContext** - Centralized data access point
6. **No Registration Endpoint** - Admin-managed user creation only
7. **Hosted Service Seeding** - Post-migration data initialization
8. **Options Pattern** - Strongly-typed configuration management
9. **JWT Authentication** - Stateless authentication for API access
10. **Global Exception Handler** - Centralized error handling and logging across all endpoints
11. **Email Notification System** - Outbox pattern with background processing
12. **Logging System** - Serilog with environment-specific sinks and database storage

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
