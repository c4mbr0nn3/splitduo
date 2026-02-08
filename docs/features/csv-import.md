# SplitDuo CSV Import Implementation Guide

## Overview

This document outlines the implementation approach for importing Cospend CSV backup files into SplitDuo using the CsvHelper library in C#. The import feature enables users to migrate their existing expense data from Cospend into SplitDuo.

## Prerequisites

Before implementing the CSV import feature, the following components must be implemented:

1. **Payment Mode Feature**: Complete implementation of PaymentMode entity, API endpoints, and database schema
2. **Payment Mode Management**: CRUD operations for payment modes in SplitDuo
3. **Expense-PaymentMode Integration**: Link expenses to payment modes in the existing expense system

The CSV import implementation depends on these features being available to properly map Cospend payment mode data.

## Cospend CSV File Structure

### File Format Analysis

Based on analysis of the sample file `cospend/ciccionetti-club_2023-11-11_export_2025-01-26.csv`, Cospend exports contain 4 distinct sections separated by empty lines:

1. **Members Section** - User information and weights
2. **Expenses Section** - All expense transactions
3. **Categories Section** - Category definitions with IDs
4. **Payment Modes Section** - Payment method definitions

### Section 1: Members

```csv
name,weight,active,color
"User1",1,1,"#499aa2"
"User2",1,1,"#d09e6d"
```

**Mapping to SplitDuo:**

- `name` → User entity (firstName, lastName parsing required)
- `weight` → Not used in SplitDuo (always 1.0 for equal splits)
- `active` → 1 = true, 0 = false (only import active users)
- `color` → Not used in SplitDuo

### Section 2: Expenses

```csv
what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
```

**Key Fields Mapping:**

- `what` → Expense.title
- `amount` → Expense.amount (decimal)
- `date` → Expense.expenseDate (DateOnly, format: YYYY-MM-DD)
- `payer_name` → Expense.paidByUserId (lookup by name)
- `owers` → ExpenseSplit records (comma-separated names)
- `categoryid` → Expense.categoryId (requires category mapping)
- `comment` → Expense.description
- `deleted` → Skip if 1, import if 0

**Additional Mappings (requires PaymentMode feature):**

- `paymentmode` → PaymentMode lookup by name
- `paymentmodeid` → PaymentMode.id (direct mapping)

**Fields Not Used in SplitDuo:**

- `timestamp`, `payer_weight`, `payer_active`, `repeat*`

### Section 3: Categories

```csv
categoryname,categoryid,icon,color
"Bus/train",1,"","#000000"
"TV/Phone/Internet",2,"","#000000"
```

**Mapping:** Use existing category mapping from `docs/migration/cospend-category-mapping.md`

### Section 4: Payment Modes

```csv
paymentmodename,paymentmodeid,icon,color
"Credit card",1,"💳","#FF7F50"
"Cash",2,"💵","#556B2F"
```

**Mapping to SplitDuo:**

- `paymentmodename` → PaymentMode.name
- `paymentmodeid` → PaymentMode.id (direct mapping)
- `icon` → discarded
- `color` → discarded

**Note:** Payment mode feature must be implemented as a prerequisite before CSV import implementation. This includes creating PaymentMode entity, API endpoints, and database schema.

## Implementation Architecture

### Core Components

#### 1. CsvHelper Configuration

**Required NuGet Package:**

```xml
<PackageReference Include="CsvHelper" Version="33.0.1" />
```

#### 2. DTO Classes for CSV Parsing

```csharp
// File: SplitDuo.Api/Features/Import/Dto/CospendImportDtos.cs

public class CospendMemberDto
{
    [Name("name")]
    public string Name { get; set; } = "";

    [Name("weight")]
    public decimal Weight { get; set; }

    [Name("active")]
    public int Active { get; set; }

    [Name("color")]
    public string Color { get; set; } = "";

    [Ignore]
    public bool IsActive => Active == 1;
}

public class CospendExpenseDto
{
    [Name("what")]
    public string What { get; set; } = "";

    [Name("amount")]
    public decimal Amount { get; set; }

    [Name("date")]
    public string Date { get; set; } = "";

    [Name("payer_name")]
    public string PayerName { get; set; } = "";

    [Name("owers")]
    public string Owers { get; set; } = "";

    [Name("categoryid")]
    public int CategoryId { get; set; }

    [Name("comment")]
    public string Comment { get; set; } = "";

    [Name("deleted")]
    public int Deleted { get; set; }

    [Ignore]
    public bool IsDeleted => Deleted == 1;

    [Ignore]
    public DateOnly ParsedDate => DateOnly.Parse(Date);

    [Ignore]
    public List<string> OwersNames =>
        Owers.Split(',', StringSplitOptions.RemoveEmptyEntries)
              .Select(name => name.Trim())
              .ToList();
}
```

#### 3. Import Service Structure

**Service Location:** `SplitDuo.Api/Features/Import/Services/CospendImportService.cs`

**Interface:**

```csharp
public interface IImportService
{
    Task<Result<ImportStatusDto>> ImportFileAsync(IFormFile file, int groupId, int userId);
}
```

**Key Methods:**

```csharp
public class CospendImportService : IImportService
{
    // Main import orchestration
    public async Task<Result<ImportStatusDto>> ImportFileAsync(IFormFile file, int groupId, int userId);

    // CSV parsing methods
    private async Task<List<CospendExpenseDto>> ParseExpensesSection(CsvReader reader);

    // Data transformation methods
    private async Task<Result<int>> CreateExpensesAsync(List<CospendExpenseDto> expenses, int groupId);

    // Helper methods
    private static Dictionary<int, ExpenseCategory> BuildCategoryMapping();
    private static Dictionary<int, PaymentMode> BuildPaymentModeMapping();
    private static Dictionary<string, int> BuildStaticUserMapping();
    private ExpenseSplit[] CalculateEqualSplits(decimal amount, List<int> userIds);
}
```

### Import Process Flow

#### 1. File Processing Pipeline

```bash
1. Upload File → 2. Parse CSV → 3. Validate Data → 4. Transform Data → 5. Save to Database
```

#### 2. Detailed Step Breakdown

**Step 1: File Upload & Validation**

- Validate file extension (.csv)
- Validate file size (max 10MB)
- Validate group exists and user has access
- Create Import entity with Pending status

**Step 2: CSV Parsing with CsvHelper**

```csharp
public async Task<Result<ImportStatusDto>> ImportFileAsync(IFormFile file, int groupId, int userId)
{
    try
    {
        var import = new Core.Domain.Entities.Import
        {
            FileName = file.FileName,
            ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
            GroupId = groupId,
            UserId = userId
        };

        var reader = new CsvReader(new StreamReader(file.OpenReadStream()), CultureInfo.InvariantCulture);
        var expenses = await ParseExpensesSection(reader);
        var result = await CreateExpensesAsync(expenses, groupId);
        if (result.IsFailure) throw new Exception(result.Error);

        import.RecordsCount = result.Value;
        import.Status = ImportStatus.Completed;
        await unitOfWork.Imports.AddAsync(import);

        var response = new ImportStatusDto(import);
        return Result<ImportStatusDto>.Success(response);
    }
    catch (Exception e)
    {
        logger.LogError(e, "An error occured while importing Cospend file");
        return Result<ImportStatusDto>.InternalServerError(e.Message);
    }
}
```

**Step 3: Data Validation**

- Validate expense dates are parseable
- Validate amounts are positive
- Check for required fields

**Step 4: Data Transformation**

**Static User Mapping (MVP Approach):**

```csharp
private static Dictionary<string, int> BuildStaticUserMapping()
{
    return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "Francesco", 1 },
        { "Beatrice", 2 }
    };
}
```

**Expense Creation:**

```csharp
private async Task<Result<int>> CreateExpensesAsync(List<CospendExpenseDto> expenses, int groupId)
{
    var recordsCount = 0;
    var categoryMapping = BuildCategoryMapping();
    var paymentModeMapping = BuildPaymentModeMapping();
    var userNameMapping = BuildStaticUserMapping();

    foreach (var exp in expenses.Where(e => !e.IsDeleted))
    {
        // Map category
        var category = categoryMapping.GetValueOrDefault(exp.CategoryId, ExpenseCategory.Other);

        // Map payment mode
        var paymentMode = paymentModeMapping.GetValueOrDefault(exp.PaymentModeId, PaymentMode.Other);

        // Find payer using static mapping
        // Skip if payer not found in static mapping
        if (!userNameMapping.TryGetValue(exp.PayerName, out var payerId)) continue;

        // Create expense
        var expense = new Expense
        {
            GroupId = groupId,
            Title = exp.What,
            Description = exp.Comment,
            Amount = exp.Amount,
            PaidBy = payerId,
            ExpenseDate = exp.ParsedDate,
            Category = category,
            PaymentMode = paymentMode
        };

        await unitOfWork.Expenses.AddAsync(expense);
        await unitOfWork.SaveChangesAsync(); // Need ID for splits

        // Create splits
        var owersUserIds = exp.OwersNames
            .Select(name => userNameMapping.GetValueOrDefault(name))
            .Where(id => id > 0)
            .ToList();

        if (owersUserIds.Count == 0) throw new Exception("No users were found for expense");

        var splits = CalculateEqualSplits(expense.Amount, owersUserIds);
        foreach (var split in splits)
        {
            split.ExpenseId = expense.Id;
        }

        await unitOfWork.ExpenseSplits.AddRangeAsync(splits);
        await unitOfWork.SaveChangesAsync();
        recordsCount++;
    }

    return Result<int>.Success(recordsCount);
}
```

**Step 5: Database Transaction**

- Wrap entire import in database transaction
- Update Import entity status (Completed/Failed)
- Record statistics (records imported, errors)

### Payment Mode Mapping Implementation

```csharp
private static Dictionary<int, PaymentMode> BuildPaymentModeMapping()
{
    return new Dictionary<int, PaymentMode>
    {
        { 1, PaymentMode.Card },
        { 2, PaymentMode.Cash },
        { 3, PaymentMode.Other },
        { 4, PaymentMode.Transfer },
        { 5, PaymentMode.OnlineService }
    };
}
```

### Category Mapping Implementation

```csharp
private static Dictionary<int, ExpenseCategory> BuildCategoryMapping()
{
    return new Dictionary<int, ExpenseCategory>
    {
        // Based on docs/cospend_category_mapping.md
        { 5, ExpenseCategory.Groceries },
        { 15, ExpenseCategory.Groceries },
        { 9, ExpenseCategory.Groceries },
        { 6, ExpenseCategory.Dining },
        { 4, ExpenseCategory.Transportation },
        { 1, ExpenseCategory.Transportation },
        { 7, ExpenseCategory.Transportation },
        { 27, ExpenseCategory.Transportation },
        { 8, ExpenseCategory.Transportation },
        { 30, ExpenseCategory.Transportation },
        { 32, ExpenseCategory.Transportation },
        { 2, ExpenseCategory.Utilities },
        { 21, ExpenseCategory.Utilities },
        { 19, ExpenseCategory.Utilities },
        { 33, ExpenseCategory.Utilities },
        { 12, ExpenseCategory.Utilities },
        { 25, ExpenseCategory.Housing },
        { 16, ExpenseCategory.Housing },
        { 24, ExpenseCategory.Housing },
        { 35, ExpenseCategory.Housing },
        { 11, ExpenseCategory.Entertainment },
        { 28, ExpenseCategory.Entertainment },
        { 31, ExpenseCategory.Entertainment },
        { 13, ExpenseCategory.Entertainment },
        { 26, ExpenseCategory.Shopping },
        { 14, ExpenseCategory.Shopping },
        { 20, ExpenseCategory.Shopping },
        { 17, ExpenseCategory.Shopping },
        { 34, ExpenseCategory.Shopping },
        { 36, ExpenseCategory.Travel },
        { 22, ExpenseCategory.Health },
        { 18, ExpenseCategory.Education },
        // Default mapping for unknown categories
        { 23, ExpenseCategory.Other },
        { 3, ExpenseCategory.Other },
        { 10, ExpenseCategory.Other },
        { 29, ExpenseCategory.Other },
        { 0, ExpenseCategory.Other },
        { -11, ExpenseCategory.Other }
    };
}
```

### Error Handling Strategy

#### 1. Validation Errors

- **File Format**: Not a valid CSV file
- **Missing Sections**: Required sections not found
- **Data Quality**: Invalid dates, negative amounts, missing users

#### 2. Business Logic Errors

- **Duplicate Import**: Same file already imported
- **User Conflicts**: Name matching ambiguities
- **Category Mapping**: Unknown category IDs

#### 3. Technical Errors

- **Database Errors**: Connection issues, constraint violations
- **Memory Errors**: Large file processing
- **Timeout Errors**: Long-running imports

### API Integration

#### Controller Implementation

```csharp
[Route("api/v1/groups/{groupId}/imports")]
[Authorize]
public class ImportController : BaseApiController
{
    [HttpPost("cospend")]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> ImportCospendFile(
        string groupId,
        [FromForm] ImportRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return HandleResult(Result<ImportStatusDto>.Unauthorized());

        var result = await _importService.ImportCospendFileAsync(groupId, request.File, userId.Value);

        if (result.IsSuccess)
            await _unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Import completed successfully");
    }

    [HttpGet("{importId}/status")]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> GetImportStatus(string importId)
    {
        var result = await _importService.GetImportStatusAsync(importId);
        return HandleResult(result);
    }
}
```

### Future Enhancements

#### 1. Async Processing

- Queue imports for background processing
- Provide progress updates via SignalR
- Email notifications on completion

#### 2. Advanced Features

- Preview mode (validate without importing)
- Mapping customization UI
- Duplicate detection and merging

#### 3. Export Compatibility

- Export in Cospend format
- Data integrity validation

## Implementation Checklist

### Phase 1: Core Infrastructure

- [ ] Add CsvHelper NuGet package
- [ ] Create DTO classes for CSV parsing
- [ ] Implement ImportService interface
- [ ] Set up database transaction handling

### Phase 2: CSV Parsing Logic

- [ ] Implement section parsing methods
- [ ] Add CSV configuration for CultureInfo
- [ ] Handle empty sections gracefully
- [ ] Validate with sample Cospend files

### Phase 3: Data Transformation

- [ ] Implement category mapping
- [ ] Create static user mapping
- [ ] Build expense and split creation
- [ ] Add data validation rules

### Phase 4: Error Handling & Completion

- [ ] Add comprehensive error handling
- [ ] Complete data validation rules

### Phase 5: API Integration

- [ ] Update ImportController
- [ ] Add basic file validation

This implementation guide provides the foundation for creating a robust CSV import feature that can successfully migrate Cospend data to SplitDuo while maintaining data integrity and providing a good user experience.
