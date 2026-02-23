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
│   ├── Categories/
│   │   ├── Controllers/
│   │   └── Dto/
│   ├── PaymentModes/
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

    public async Task<Result<UserDto>> GetUserAsync(Guid userId)
    {
        var user = await _unitOfWork.Users.FindByGuidAsync(userId);
        if (user == null)
            return Result<UserDto>.NotFound("User not found");

        return Result<UserDto>.Success(new UserDto(user));
    }
}

// Controller with centralized Result handling
public class UsersController : BaseApiController
{
    private readonly UsersService _usersService;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<ActionResult<ApiResponseDto<UserDto>>> GetUser(Guid userId)
    {
        var result = await _usersService.GetUserAsync(userId);
        return HandleResult(result);
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

### BaseApiController with UserContext Integration

**Location**: `SplitDuo.Api/Features/Common/Controllers/BaseApiController.cs`

The `BaseApiController` provides centralized Result handling and user context access, eliminating repetitive response mapping code in controllers.

**Features:**

- **Automatic Status Code Mapping**: Converts Result status codes to appropriate ActionResult responses
- **Consistent API Responses**: Maintains standardized response format using `ApiResponseDto<T>`
- **Error Code Generation**: Automatically generates error codes from HTTP status codes
- **Generic Support**: Handles both `Result<T>` and non-generic `Result` types
- **User Context Access**: Built-in access to current user information via UserContextService
- **Authentication Helpers**: Protected methods for common authentication checks

**Controller Base Implementation:**

```csharp
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    private IUserContextService? _userContextService;

    private IUserContextService UserContextService =>
        _userContextService ??= HttpContext.RequestServices.GetRequiredService<IUserContextService>();

    protected Guid? GetCurrentUserId() => UserContextService.GetCurrentUserId();
    protected Task<User?> GetCurrentUserAsync() => UserContextService.GetCurrentUserAsync();
    protected bool IsUserAuthenticated() => UserContextService.IsAuthenticated();

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

    [HttpPost("revoke")]
    [Authorize]
    public async Task<ActionResult> RevokeToken([FromBody] RefreshTokenRequestDto request)
    {
        var userId = GetCurrentUserId(); // Using BaseApiController method
        if (userId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        var result = await _authenticationService.RevokeTokenAsync(request.RefreshToken, userId.Value);
        return HandleResult(result, "Token revoked successfully");
    }
}
```

**Groups Controller Example:**

```csharp
[Route("api/v1/groups")]
public class GroupsController(
    IGroupsService groupsService,
    IUnitOfWork unitOfWork,
    ILogger<GroupsController> logger) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<GroupDto>>>> GetUserGroups()
    {
        var currentUserId = GetCurrentUserId(); // BaseApiController method
        if (currentUserId == null)
            return HandleResult(Result<List<GroupDto>>.Unauthorized("User not authenticated"));

        var result = await groupsService.GetUserGroupsAsync(currentUserId.Value);
        return HandleResult(result, "User groups retrieved successfully");
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<GroupDto>>> CreateGroup([FromBody] CreateGroupRequestDto request)
    {
        logger.LogInformation("Creating group: {GroupName}", request.Name);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<GroupDto>.Unauthorized("User not authenticated"));

        var result = await groupsService.CreateGroupAsync(currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(); // Controller handles database commits

        return HandleResult(result, "Group created successfully");
    }

    [HttpDelete("{groupId}")]
    public async Task<ActionResult> DeleteGroup(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        var result = await groupsService.DeleteGroupAsync(groupId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Group deleted successfully");
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

## User Context Service

### UserContextService Implementation

**Location**: `SplitDuo.Api/Features/Common/Services/UserContextService.cs`

The `UserContextService` provides centralized access to current user information from HTTP context, eliminating the need for manual JWT claims parsing in controllers and services.

### Service Interface

```csharp
public interface IUserContextService
{
    Guid? GetCurrentUserId();
    Task<User?> GetCurrentUserAsync();
    bool IsAuthenticated();
}
```

### Implementation Features

**JWT Claims Integration**:

- Extracts user ID from `"userId"` claim in JWT token
- Handles null/invalid claims gracefully
- Uses `IHttpContextAccessor` for HTTP context access

**Database Integration**:

- `GetCurrentUserAsync()` queries database using `IUnitOfWork`
- Returns full `User` entity based on GUID from JWT claims
- Efficient single query with automatic caching by EF Core

**Authentication Check**:

- `IsAuthenticated()` verifies current request has valid authentication
- Checks `HttpContext.User.Identity.IsAuthenticated` property

### BaseApiController Integration

**Service Access Pattern**:

```csharp
public abstract class BaseApiController : ControllerBase
{
    private IUserContextService? _userContextService;

    private IUserContextService UserContextService =>
        _userContextService ??= HttpContext.RequestServices.GetRequiredService<IUserContextService>();
}
```

**Protected Helper Methods**:

- `GetCurrentUserId()` - Returns current user's GUID or null
- `GetCurrentUserAsync()` - Returns full User entity or null
- `IsUserAuthenticated()` - Returns authentication status

### Usage Patterns

**In Controllers (via BaseApiController)**:

```csharp
[Authorize]
[HttpGet("profile")]
public async Task<ActionResult<ApiResponseDto<UserDto>>> GetCurrentUser()
{
    var userId = GetCurrentUserId();
    if (userId == null)
        return HandleResult(Result.Unauthorized("User not authenticated"));

    var user = await GetCurrentUserAsync();
    if (user == null)
        return HandleResult(Result.NotFound("User not found"));

    return HandleResult(Result.Success(userDto), "User retrieved successfully");
}
```

**Direct Service Injection**:

```csharp
public class ExpensesService(IUnitOfWork unitOfWork, IUserContextService userContext)
{
    public async Task<Result<ExpenseDto>> CreateExpenseAsync(CreateExpenseRequestDto request)
    {
        var currentUserId = userContext.GetCurrentUserId();
        if (currentUserId == null)
            return Result<ExpenseDto>.Unauthorized("User not authenticated");

        // Use currentUserId for authorization checks and audit trails
        var expense = new Expense
        {
            CreatedBy = currentUserId.Value,
            // ... other properties
        };

        unitOfWork.Expenses.Add(expense);
        return Result<ExpenseDto>.Success(expenseDto);
    }
}
```

### Security Features

**Claims Validation**:

- Validates GUID format from JWT claims
- Returns null for invalid or missing user IDs
- No exceptions thrown for malformed data

**Authentication Requirements**:

- Works with `[Authorize]` attribute for endpoint protection
- Gracefully handles unauthenticated requests
- Provides clear authentication status checks

**Database Consistency**:

- User lookups use GUID from JWT (not integer ID)
- Handles cases where JWT user doesn't exist in database
- Returns null rather than throwing exceptions

### Registration & Dependencies

**Dependency Injection Registration**:

```csharp
// In ApiProgramExtensions.cs
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddScoped<IGroupsService, GroupsService>();
builder.Services.AddScoped<IExpensesService, ExpensesService>();
builder.Services.AddHttpContextAccessor(); // Already registered in Core
```

**Service Dependencies**:

- `IHttpContextAccessor` - For accessing current HTTP context
- `IUnitOfWork` - For database user lookups
- Registered as Scoped lifetime for per-request consistency

### Benefits

**Centralized User Access**:

- Single point of access for current user information
- Consistent user ID extraction across all controllers
- Eliminates duplicate JWT parsing code

**Simplified Controllers**:

- No need to inject `IUserContextService` directly when using `BaseApiController`
- Clean, readable controller methods
- Reduced constructor parameters

**Enhanced Security**:

- Centralized validation of user claims
- Consistent handling of authentication edge cases
- Clear separation between authentication and authorization logic

**Maintainability**:

- Changes to user context logic centralized in one service
- Easy to modify JWT claim names or structure
- Simplified unit testing with mockable interface

### Required Services for v1.0 MVP

The following services are needed to implement the core features outlined in the project specification:

#### Core Business Services

1. **AuthenticationService** _(implemented)_
   - **Secure Authentication**: User login with credential validation
   - **JWT Token Management**: Short-lived access tokens (15 min) with JTI claims
   - **Refresh Token System**: Cryptographically secure refresh tokens with rotation
   - **Token Revocation**: Individual and bulk token revocation capabilities
   - **Security Features**: Breach detection, audit logging, token reuse prevention
   - **Two-Factor Authentication**: Integrated 2FA support with TOTP, email codes, and backup codes
   - **Location**: `SplitDuo.Api/Features/Authentication/Services/AuthenticationService.cs`
   - **Database**: RefreshToken entity for secure server-side token storage
   - **Features**: Enhanced Result pattern with HTTP status codes, Unit of Work data access, ASP.NET Core Identity password hashing, 2FA login flow integration

2. **UsersService** _(implemented)_
   - **User Management**: User listing, profile operations, and admin CRUD
   - **Profile Operations**: Update user profiles (current user and admin operations)
   - **Password Management**: Change password with current password verification
   - **CRUD Operations**: Get users, individual user retrieval, soft delete operations
   - **Location**: `SplitDuo.Api/Features/Users/Services/UsersService.cs`
   - **Features**: Enhanced Result pattern with HTTP status codes, UserDto constructor mapping, Unit of Work data access
   - **Password Generation**: Cryptographically secure passwords (uppercase, lowercase, digits, special chars) with Fisher-Yates shuffle

3. **GroupsService** _(implemented)_
   - **Group Management**: Create, read, update, and delete groups with comprehensive authorization
   - **Member Management**: Add/remove group members with email-based invitation and role assignment
   - **Access Control**: Group membership validation, admin-only operations, self-removal permissions
   - **Business Rules**: Creator automatically becomes admin, soft delete operations, duplicate prevention
   - **Security Features**: Member authorization checks, admin permission validation, protection against removing last admin
   - **Location**: `SplitDuo.Api/Features/Groups/Services/GroupsService.cs`
   - **Features**: Enhanced Result pattern with HTTP status codes, Unit of Work data access, comprehensive DTO mapping

4. **ExpensesService** _(implemented)_
   - **Expense Management**: Complete CRUD operations with comprehensive validation and business rule enforcement
   - **Paginated Retrieval**: Advanced filtering by date range, category, and user with efficient pagination
   - **Split Calculation**: Automatic expense split creation with amount-based and percentage-based distribution
   - **Business Logic**: Split validation (sum equals expense amount), member participation validation, category management
   - **Authorization**: Group membership validation, multi-layered access control, member-only expense access
   - **Performance**: Optimized EF Core queries with includes, efficient split loading, proper indexing utilization
   - **Location**: `SplitDuo.Api/Features/Expenses/Services/ExpensesService.cs`
   - **Features**: Enhanced Result pattern with HTTP status codes, comprehensive DTO mapping, transaction safety

5. **BalancesService** _(implemented)_
   - **Balance Calculations**: Calculate who owes what to whom across all group members
   - **Settlement Optimization**: Generate optimal settlement suggestions using greedy algorithm to minimize transactions
   - **Balance Summaries**: Provide comprehensive balance overviews with settlement recommendations
   - **Multi-Source Calculations**: Integrates expenses and settlements data for accurate balance computation
   - **Service Integration**: Uses SettlementsService for settlement data via dedicated balance calculation method
   - **Location**: `SplitDuo.Api/Features/Expenses/Services/BalancesService.cs`
   - **Features**: Enhanced Result pattern, separation of concerns architecture, debt optimization algorithms
   - **API Methods**: 2 distinct service methods for balance retrieval and summary generation with settlement suggestions

6. **SettlementsService** _(implemented)_
   - **Settlement Management**: Complete CRUD operations for recording payments between group members
   - **Paginated Retrieval**: Advanced filtering by date range with efficient pagination support
   - **Authorization**: Multi-layered group membership validation and user permission enforcement
   - **Business Logic**: Payment validation, user membership verification, amount and date validation
   - **Data Separation**: Dedicated balance calculation method for service-to-service communication
   - **Location**: `SplitDuo.Api/Features/Settlements/Services/SettlementsService.cs`
   - **Features**: Enhanced Result pattern, comprehensive validation, internal service communication patterns
   - **API Methods**: 5 distinct service methods covering settlement lifecycle with balance calculation support

7. **TwoFactorService** _(implemented)_
   - **2FA Management**: Complete two-factor authentication lifecycle management
   - **TOTP Support**: RFC 6238 compliant Time-based One-Time Password implementation compatible with Google Authenticator, Authy
   - **Email Verification**: Time-limited email verification codes with rate limiting and attempt tracking
   - **Backup Codes**: Emergency access codes with one-time use and secure hashing
   - **Setup Process**: Multi-step verification flow with QR code generation for authenticator apps
   - **Security Features**: Cryptographically secure token generation, SHA256 hashing, time drift tolerance
   - **Location**: `SplitDuo.Api/Features/Authentication/Services/TwoFactorService.cs`
   - **Database**: TwoFactorToken entity for managing verification codes and attempts
   - **Features**: Enhanced Result pattern, email integration via NotificationService, secure backup code management

#### Data Management Services

1. **ImportService**
   - Import Cospend backup files
   - Data validation and transformation
   - Batch import operations with status tracking

2. **ExportService**
   - Export data to CSV format
   - Generate Cospend-compatible backup files
   - Data formatting and serialization

#### Infrastructure Services

1. **UserContextService** _(implemented)_
   - **Current User Access**: Get authenticated user ID and entity from HTTP context
   - **Authentication Check**: Verify if current request is authenticated
   - **JWT Claims Integration**: Extract user information from JWT token claims
   - **Location**: `SplitDuo.Api/Features/Common/Services/UserContextService.cs`
   - **Integration**: Available through BaseApiController protected methods
   - **Features**: Centralized user context access, simplified authentication checks

2. **NotificationService** _(implemented)_
   - **Outbox Pattern Implementation**: Queue email notifications with database persistence
   - **Retry Logic**: Maximum 3 attempts with error tracking and logging
   - **Queue Management**: Get unsent notifications, send emails, enqueue new notifications
   - **Pruning System**: Automatic cleanup of sent notifications older than 30 days
   - **Database Integration**: Full UnitOfWork support with performance-optimized indexes
   - **Location**: `SplitDuo.Core/Services/EmailNotificationService.cs`
   - **Features**: Enhanced Result pattern, comprehensive logging, transaction safety
   - **Background Processing**: Integrated with Quartz.NET job scheduler for automatic processing

3. **EmailService** _(implemented)_
   - **SMTP Integration**: MailKit-based email sending with SSL/TLS support
   - **Error Handling**: Comprehensive exception handling with specific HTTP status codes
   - **HTML Email Support**: Rich email formatting with BodyBuilder
   - **Configuration**: Environment-based SMTP settings via SmtpOptions
   - **Location**: `SplitDuo.Core/Services/SmtpService.cs`
   - **Features**: Authentication support, connection management, detailed error categorization

#### Other Services

- **DataSeederService** - Initial user creation and optional demo data seeding from configuration _(implemented)_

#### Background Jobs

- **LogCleanupJob** - Background service for log maintenance _(implemented)_
- **EmailNotificationProcessingJob** - Background service for processing email notifications _(implemented)_
- **EmailNotificationPruneJob** - Background service for pruning old email notifications _(implemented)_

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

**UsersService Implementation:**

- **Architecture**: Service located in feature folder following Vertical Slice Architecture (`SplitDuo.Api/Features/Users/Services/`)
- **Dependencies**: Uses `IUnitOfWork` and `IPasswordHasher<User>` for secure password operations
- **DTOs**: Utilizes DTOs from `SplitDuo.Api/Features/Users/Dto/` with constructor-based entity mapping
- **Password Generation**: Cryptographically secure 12-character passwords using `RandomNumberGenerator`
  - **Complexity Requirements**: At least one uppercase, lowercase, digit, and special character
  - **Security Features**: Fisher-Yates shuffle algorithm to prevent predictable patterns
  - **Character Set**: Alphanumeric + special characters (`!@#$%^&*`)
- **Business Logic**: Email uniqueness validation, soft delete operations, profile update validation
- **Error Handling**: Enhanced Result pattern with appropriate HTTP status codes (Conflict, NotFound, Unauthorized)
- **Data Access**: Direct Unit of Work usage without repository pattern, controllers handle SaveChanges operations
- **Mapping Pattern**: UserDto constructor pattern for clean entity-to-DTO conversion

**GroupsService Implementation:**

- **Architecture**: Service located in feature folder following Vertical Slice Architecture (`SplitDuo.Api/Features/Groups/Services/`)
- **Dependencies**: Uses `IUnitOfWork` for data access and comprehensive Entity Framework Include operations
- **DTOs**: Utilizes DTOs from `SplitDuo.Api/Features/Groups/Dto/` with manual DTO mapping and nested UserInfoDto construction
- **Authorization Pattern**: Multi-layered authorization with user authentication, group membership validation, and role-based permissions
  - **Membership Validation**: Verifies user belongs to group before allowing access to group data
  - **Admin Operations**: Create, update, delete groups and manage members require admin role
  - **Self-Management**: Users can remove themselves from groups regardless of role
  - **Last Admin Protection**: Prevents removal of the only admin in a group
- **Business Logic**: Comprehensive group lifecycle management with automatic admin assignment and soft delete cascading
  - **Group Creation**: Creator automatically assigned as admin member upon group creation
  - **Member Management**: Email-based member addition with role assignment (admin/member)
  - **Soft Delete Cascade**: Group deletion soft-deletes all associated memberships
  - **Duplicate Prevention**: Prevents adding existing members to groups
- **Error Handling**: Enhanced Result pattern with contextual HTTP status codes (NotFound, Forbidden, Conflict, Unauthorized)
- **Data Access**: Advanced EF Core usage with Include operations, projection queries for performance, and transactional consistency
- **Query Optimization**: Uses projection and selective loading to minimize database round trips
- **API Methods**: Complete CRUD operations with 8 distinct service methods covering all group management scenarios

**ExpensesService Implementation:**

- **Architecture**: Service located in feature folder following Vertical Slice Architecture (`SplitDuo.Api/Features/Expenses/Services/`)
- **Dependencies**: Uses `IUnitOfWork` for comprehensive data access with advanced Entity Framework operations
- **DTOs**: Utilizes DTOs from `SplitDuo.Api/Features/Expenses/Dto/` with manual DTO mapping and nested split construction
- **Pagination Support**: Advanced pagination with filtering capabilities (date range, category, user) using efficient database queries
- **Split Management**: Comprehensive expense split handling with dual calculation modes (amount-based and percentage-based)
  - **Split Validation**: Ensures split amounts sum to expense total (±0.01 tolerance for rounding)
  - **Member Validation**: Validates all split participants are group members
  - **Split Calculation**: Automatic percentage calculation for display purposes
  - **Split Updates**: Complete split replacement strategy for updates
- **Business Logic**: Multi-layered validation and business rule enforcement
  - **Expense Categories**: Uses ExpenseCategory enum with validation and case-insensitive parsing
  - **Date Handling**: DateOnly parsing and validation for expense dates
  - **Amount Validation**: Positive amount enforcement and decimal precision handling
  - **Member Participation**: Ensures payer and split users are group members
- **Authorization Pattern**: Group membership validation with expense access control
  - **Group Access**: Verifies user membership before allowing any expense operations
  - **Expense Scope**: Limits expense access to group members only
  - **User Context**: Integrates with user authentication for current user operations
- **Performance Optimization**: Advanced EF Core query optimization strategies
  - **Efficient Loading**: Uses Include operations and projection queries
  - **Split Loading**: Optimized split loading with grouping and dictionary operations
  - **Pagination**: Database-level pagination with proper counting and ordering
  - **Index Utilization**: Leverages existing database indexes for optimal query performance
- **Error Handling**: Enhanced Result pattern with comprehensive HTTP status codes (BadRequest, Unauthorized, Forbidden, NotFound)
- **Data Operations**: Complete CRUD functionality with soft delete support
- **API Methods**: 5 distinct service methods covering expense lifecycle management with advanced filtering

**BalancesService Implementation:**

- **Architecture**: Service located in Expenses feature folder following Vertical Slice Architecture (`SplitDuo.Api/Features/Expenses/Services/`)
- **Dependencies**: Uses `IUnitOfWork` and `ISettlementsService` for separation of concerns
- **Balance Calculation Engine**: Multi-source balance computation integrating expenses and settlements
  - **Expense Integration**: Calculates amounts paid by users vs. amounts owed through expense splits
  - **Settlement Integration**: Incorporates settlement payments through dedicated SettlementsService method
  - **Member Filtering**: Processes only active group members with proper navigation property loading
  - **Final Balance**: Computed as `TotalPaid - TotalOwed` (positive = owed money, negative = owes money)
- **Settlement Optimization Algorithm**: Debt settlement optimization using greedy algorithm
  - **Creditor/Debtor Separation**: Identifies users with positive vs. negative balances
  - **Queue-Based Matching**: Uses priority queues to pair largest creditors with largest debtors
  - **Transaction Minimization**: Generates minimum number of payments to settle all debts
  - **Suggestion Generation**: Creates `BalanceSuggestionDto` with user-friendly payment descriptions
- **Authorization Pattern**: Group membership validation with consistent user authentication
- **Service Integration**: Proper separation of concerns using SettlementsService for settlement data
  - **Internal Communication**: Uses `GetSettlementsForBalanceCalculationAsync` for data access
  - **Data Transfer**: Uses `SettlementBalanceData` DTO for minimal data transfer
  - **Loose Coupling**: No direct database access to settlement entities
- **Error Handling**: Enhanced Result pattern with HTTP status codes (BadRequest, Unauthorized, Forbidden, NotFound)
- **API Methods**: 2 distinct service methods for balance retrieval and summary with optimization suggestions

**SettlementsService Implementation:**

- **Architecture**: Service located in feature folder following Vertical Slice Architecture (`SplitDuo.Api/Features/Settlements/Services/`)
- **Dependencies**: Uses `IUnitOfWork` for comprehensive data access with Entity Framework operations
- **Settlement Management**: Complete CRUD operations for payment recording between group members
  - **Payment Validation**: Ensures positive amounts, valid dates, and prevents self-payments
  - **User Verification**: Validates both from/to users exist and are active group members
  - **Business Rules**: Enforces payment constraints and data integrity
- **Pagination Support**: Advanced pagination with date range filtering capabilities
  - **Date Filtering**: Supports `startDate` and `endDate` parameters for settlement history
  - **Efficient Queries**: Database-level pagination with proper ordering by settlement date
  - **Response Metadata**: Complete pagination information with total counts and navigation flags
- **Authorization Pattern**: Multi-layered group membership validation
  - **Group Access**: Verifies user membership before allowing any settlement operations
  - **Member Validation**: Ensures settlement participants are active group members
  - **User Context**: Integrates with authentication for current user operations
- **Service-to-Service Communication**: Dedicated method for balance calculations
  - **Balance Integration**: `GetSettlementsForBalanceCalculationAsync` provides settlement data to BalancesService
  - **Data Projection**: Uses efficient SELECT projection to minimize data transfer
  - **Internal Interface**: Separation between public API methods and internal service communication
- **Business Logic**: Comprehensive validation and business rule enforcement
  - **Date Handling**: DateOnly parsing and validation for settlement dates
  - **Amount Validation**: Positive amount enforcement and decimal precision handling
  - **Update Operations**: Partial update support for settlement modifications
- **Performance Optimization**: EF Core query optimization with Include operations and proper indexing
- **Error Handling**: Enhanced Result pattern with comprehensive HTTP status codes (BadRequest, Unauthorized, Forbidden, NotFound, Conflict)
- **Data Operations**: Complete CRUD functionality with soft delete support
- **API Methods**: 5 distinct service methods covering settlement lifecycle with balance calculation support

**NotificationService (EmailNotificationService) Implementation:**

- **Architecture**: Core infrastructure service (`SplitDuo.Core/Services/EmailNotificationService.cs`)
- **Dependencies**: Uses `IUnitOfWork`, `ISmtpService`, and `ILogger<EmailNotificationService>`
- **Outbox Pattern**: Database-backed notification queue with persistent retry logic
- **Retry Mechanism**: Maximum 3 attempts per notification with comprehensive error tracking
- **Database Schema**: Notification entity with indexes for performance (SentAt, CreatedAt, RetryCount)
- **Queue Processing Methods**:
  - `GetUnsentNotifications()` filters `SentAt IS NULL AND RetryCount < 3`
  - `GetPrunableNotifications()` identifies notifications older than 30 days for cleanup
  - `SendAsync()` handles email delivery with retry logic and status updates
  - `EnqueueAsync()` adds notifications to database queue
  - `Prune()` removes old notifications from database
- **Error Tracking**: Comprehensive error message storage and logging at Info/Error/Debug levels
- **Transaction Safety**: Notifications queued within same transaction as business operations
- **Status Management**: `SentAt` timestamp indicates successful delivery
- **Background Processing**: Fully integrated with Quartz.NET job system for automatic processing and cleanup

**EmailService (SmtpService) Implementation:**

- **Architecture**: Core infrastructure service (`SplitDuo.Core/Services/SmtpService.cs`)
- **Dependencies**: Uses `IOptions<SmtpOptions>` for configuration management
- **SMTP Library**: MailKit for robust email delivery with SSL/TLS support
- **Error Categorization**: Specific exception handling with appropriate HTTP status codes:
  - `AuthenticationException` → 401 Unauthorized (SMTP credentials)
  - `SmtpCommandException` → 400 Bad Request (SMTP protocol errors)
  - `SocketException` → 503 Service Unavailable (connection failures)
  - `ParseException` → 400 Bad Request (malformed email addresses)
  - General exceptions → 500 Internal Server Error
- **Email Format**: HTML email support via MimeKit BodyBuilder
- **Configuration**: Environment variable-based SMTP settings (host, port, credentials, SSL)
- **Connection Management**: Proper connection lifecycle with authentication and cleanup
- **Enhanced Result Pattern**: Consistent error handling with detailed error messages

**Background Job System Implementation:**

- **Job Scheduler**: Quartz.NET framework for reliable job scheduling and execution
- **EmailNotificationProcessingJob**: Processes notification queue with configurable intervals
  - **Concurrency Control**: `[DisallowConcurrentExecution]` ensures single job execution
  - **Processing Logic**: Retrieves unsent notifications and processes them sequentially
  - **Error Handling**: Failed notifications continue processing queue, comprehensive logging
  - **Transaction Management**: SaveChanges called after each successful email send
- **EmailNotificationPruneJob**: Maintains database performance through automatic cleanup
  - **Pruning Logic**: Removes notifications that are sent or have reached maximum retry count
  - **Age Threshold**: 30-day retention policy for processed notifications
  - **Database Optimization**: Prevents notification table growth and maintains query performance
- **Job Registration**: Configured through Quartz.NET DI integration with scoped service access
- **Monitoring**: Comprehensive logging for job execution, success rates, and failure tracking

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
- Direct DbSet exposure: `Users`, `Groups`, `GroupMembers`, `Expenses`, `ExpenseSplits`, `Settlements`, `Imports`, `TwoFactorTokens`
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

### Secure JWT-Based Authentication with Refresh Tokens

The application implements a modern, secure authentication system using short-lived access tokens with refresh token rotation.

#### Authentication Flow

```bash
1. Login → Access Token (15 min) + Refresh Token (7 days)
2. API Calls → Bearer Access Token
3. Token Expires → Refresh Token + Expired Access Token → New Tokens
4. Logout/Revoke → Refresh Token Invalidation
```

#### Token Architecture

**Access Tokens (JWT)**:

- **Expiration**: 15 minutes (configurable via `Jwt:Expires`)
- **Purpose**: API authorization for individual requests
- **Claims**: User ID, email, name, JWT ID (JTI with GUID v7)
- **Storage**: Client-side (memory/secure storage)

**Refresh Tokens**:

- **Expiration**: 7 days (configurable)
- **Purpose**: Generate new access tokens without re-login
- **Security**: Cryptographically secure 64-byte random tokens
- **Storage**: Server-side database with SHA256 hashing
- **Rotation**: New refresh token generated on each use (one-time use)

#### Database Storage

**RefreshToken Entity** (`SplitDuo.Core/Domain/Entities/RefreshToken.cs`):

```csharp
[Table("refresh_tokens")]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(UserId), nameof(RevokedAt))]
[Index(nameof(ExpiresAt))]
[Index(nameof(JwtId))]
public class RefreshToken : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }              // Foreign key to User.Id
    [Column("token_hash"), MaxLength(255)] public string TokenHash { get; set; } = "";
    [Column("jwt_id"), MaxLength(255)] public string JwtId { get; set; } = "";    // GUID v7 for ordering
    [Column("expires_at")] public long ExpiresAt { get; set; }       // Unix timestamp seconds
    [Column("revoked_at")] public long? RevokedAt { get; set; }      // Unix timestamp seconds
    [Column("revoked_reason"), MaxLength(255)] public string? RevokedReason { get; set; }
    [Column("replaced_by_token"), MaxLength(255)] public string? ReplacedByToken { get; set; }
    [Column("client_info"), MaxLength(255)] public string ClientInfo { get; set; } = "";

    // Navigation properties
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;

    // Computed properties
    [NotMapped] public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt;
    [NotMapped] public bool IsRevoked => RevokedAt.HasValue;
    [NotMapped] public bool IsActive => !IsRevoked && !IsExpired;
}
```

#### Security Features

**Token Rotation**:

- Each refresh generates new access + refresh tokens
- Old refresh token immediately invalidated
- Prevents replay attacks and token reuse

**Breach Detection**:

- Revoked/expired token usage triggers security response
- All user tokens revoked when suspicious activity detected
- Comprehensive audit logging for forensic analysis

**Cryptographic Security**:

- Refresh tokens: `RandomNumberGenerator` (64-byte secure random)
- Database storage: SHA256 hashed tokens
- JWT signing: HMAC-SHA256 with configurable secret key

#### API Endpoints

**Authentication Endpoints**:

- `POST /api/v1/auth/login` - User authentication with token generation
- `POST /api/v1/auth/refresh` - Token refresh with rotation
- `POST /api/v1/auth/revoke` - Individual token revocation

**Token Management**:

- Individual refresh token revocation
- Bulk user token revocation (security breach response)
- Automatic cleanup of expired tokens

#### Implementation Details

**AuthenticationService** (`SplitDuo.Api/Services/AuthenticationService.cs`):

```csharp
// Login flow
public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request)
{
    // 1. Validate user credentials
    // 2. Generate JWT with unique JTI (GUID v7 for ordering)
    // 3. Create cryptographically secure refresh token
    // 4. Store hashed refresh token in database
    // 5. Return both tokens to client
}

// Refresh flow
public async Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request)
{
    // 1. Validate expired JWT structure and extract claims
    // 2. Verify refresh token exists and is active in database
    // 3. Check JWT ID correlation between tokens
    // 4. Revoke old refresh token (token rotation)
    // 5. Generate new access token and refresh token
    // 6. Store new hashed refresh token
    // 7. Return new tokens to client
}
```

### Security Considerations

**Enhanced Security Measures**:

- **Short-lived access tokens** (15 minutes) minimize exposure window
- **Token rotation** prevents refresh token replay attacks
- **Database storage** enables centralized revocation and audit
- **Breach detection** automatically revokes compromised token families
- **Cryptographic hashing** protects stored refresh tokens
- **Audit trail** tracks token lifecycle for security analysis

**Password Security**:

- Password hashing using ASP.NET Core Identity `PasswordHasher<User>`
- Bearer token authentication for API access
- No self-registration endpoint - new users onboard via the invitation system
- Individual password change capabilities with proper validation

**Configuration Security**:

- JWT secrets via environment variables in production
- Configurable token expiration times
- Secure configuration via Options pattern

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
4. **Secure JWT Authentication with Refresh Tokens** - Short-lived access tokens (15min) with cryptographically secure refresh token rotation
5. **RefreshToken Database Storage** - Server-side token storage with SHA256 hashing, revocation, and audit capabilities
6. **Unit of Work Pattern** - Centralized transaction and save operations management
7. **Single DbContext** - Centralized data access point
8. **Invitation System** - Email-based user onboarding with secure token registration
9. **Hosted Service Seeding** - Post-migration data initialization
10. **Options Pattern** - Strongly-typed configuration management
11. **Global Exception Handler** - Centralized error handling and logging across all endpoints
12. **Email Notification System** - Outbox pattern with background processing
13. **Logging System** - Serilog with environment-specific sinks and database storage
14. **Groups Service Implementation** - Comprehensive group management with multi-layered authorization, role-based permissions, and advanced EF Core usage
15. **Expenses Service Implementation** - Complete expense lifecycle management with split calculation, pagination, advanced filtering, and business rule enforcement

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

**Notifications Table Schema:**

```csharp
[Table("notifications")]
[Index(nameof(SentAt))]                              // For unsent notifications query
[Index(nameof(CreatedAt))]                           // For cleanup operations
[Index(nameof(CreatedAt), nameof(SentAt))]          // For monitoring/reporting queries
[Index(nameof(SentAt), nameof(RetryCount))]         // For failed notifications with retry < 3
public class Notification
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("to")] public string To { get; set; } = "";
    [Column("subject")] public string Subject { get; set; } = "";
    [Column("body")] public string Body { get; set; } = "";
    [Column("created_at")] public long CreatedAt { get; set; }
    [Column("sent_at")] public long? SentAt { get; set; }
    [Column("retry_count")] public int RetryCount { get; set; } = 0;
    [Column("error_message")] public string? ErrorMessage { get; set; }
}
```

**Database Features:**

- **Transactional Consistency** - Notifications queued in same transaction as business data
- **Status Tracking** - `SentAt` null = pending, populated = sent
- **Retry Logic** - Maximum 3 retry attempts with `RetryCount` tracking
- **Error Tracking** - `ErrorMessage` stores last failure reason
- **Performance Indexes** - Optimized for common query patterns
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

### Service Implementation

**EmailNotificationService** (`SplitDuo.Api/Services/EmailNotificationService.cs`):

```csharp
public interface INotificationService
{
    Task<Result<List<Notification>>> GetUnsentNotifications();
    Task<Result> SendAsync(Notification notification);
    Task<Result> EnqueueAsync(Notification notification);
}
```

**Key Methods:**

- **`GetUnsentNotifications()`** - Returns notifications where `SentAt IS NULL AND RetryCount < 3`
- **`SendAsync()`** - Attempts to send email via SMTP, handles retry logic and error tracking
- **`EnqueueAsync()`** - Adds new notifications to the database queue

**SmtpService** (`SplitDuo.Core/Services/SmtpService.cs`):

- Uses MailKit for SMTP operations
- Environment-based configuration via `SmtpOptions`
- Comprehensive error handling with specific HTTP status codes
- Supports HTML email bodies

### Retry Logic & Error Handling

**Retry Behavior:**

1. **Fresh Notification**: `RetryCount = 0`, ready for processing
2. **First Failure**: `RetryCount = 1`, error stored in `ErrorMessage`
3. **Second Failure**: `RetryCount = 2`, error updated
4. **Third Failure**: `RetryCount = 3`, final attempt
5. **Max Retries Reached**: Notification excluded from `GetUnsentNotifications()`

**Error Categories:**

- **Authentication Failures**: 401 Unauthorized (SMTP credentials)
- **Command Errors**: 400 Bad Request (SMTP protocol errors)
- **Network Issues**: 503 Service Unavailable (connection failures)
- **Invalid Emails**: 400 Bad Request (malformed addresses)
- **General Failures**: 500 Internal Server Error (unexpected errors)

**Logging:**

- **Info Level**: Send attempts with retry count
- **Error Level**: Failures with detailed error messages
- **Debug Level**: Queue operations

### SMTP Configuration

**Environment Variables:**

```bash
SD_EMAIL_SENDER_NAME="SplitDuo"
SD_EMAIL_SENDER_ADDRESS="noreply@splitduo.app"
SD_EMAIL_SMTP_HOST="localhost"
SD_EMAIL_SMTP_PORT="1025"
SD_EMAIL_SMTP_USERNAME="any"
SD_EMAIL_SMTP_PASSWORD=""
SD_EMAIL_SSL="false"
```

### Implementation Benefits

- **Reliability** - Notifications survive application restarts with retry logic
- **Performance** - Non-blocking business operations with background processing
- **Monitoring** - Comprehensive error tracking and logging
- **Resilience** - Automatic retry with backoff for transient failures
- **Scalability** - Background processing prevents bottlenecks
- **Observability** - Detailed logging for debugging and monitoring

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
