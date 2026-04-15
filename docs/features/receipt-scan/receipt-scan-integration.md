# Receipt Scan — Integration Map

Maps every piece of the feature to the existing project structure. No implementation details — locations and requirements only.

---

## Backend (`sd-backend/`)

### 1. Configuration — `SplitDuo.Core/Options/`

**New file:** `AiOptions.cs`

- Follows the Options pattern used by `JwtOptions`, `SmtpOptions`, etc.
- Binds `SD_AI_BASE_URL`, `SD_AI_API_KEY`, `SD_AI_MODEL` from environment
- Exposes `IsEnabled` bool (`BaseUrl` + `Model` both non-empty)
- Registered in `SplitDuo.Core/Extensions/ApiProgramExtensions.cs` alongside other options

**New file:** `AiOptions.Setup/ConfigureAiOptions.cs` (or inline in the Options class)

- `IConfigureOptions<AiOptions>` implementation, same pattern as existing option setup classes

---

### 2. DTO — `SplitDuo.Api/Features/Receipts/Dto/`

**New file:** `ParsedReceiptDto.cs`

Fields:
- `string Title`
- `decimal Amount`
- `string? Description`
- `string ExpenseDate` (YYYY-MM-DD)
- `int? CategoryId` (valid `ExpenseCategory` enum value or null)
- `int? PaymentModeId` (valid `PaymentMode` enum value or null)

---

### 3. Service — `SplitDuo.Core/Services/`

**New file:** `ReceiptParserService.cs`

- Depends on `IOptions<AiOptions>` and `HttpClient`
- Single public method: `ParseReceiptAsync(Stream imageStream)` → `Result<ParsedReceiptDto>`
- Returns `Result.Failure` on: module disabled, HTTP error, timeout, JSON parse failure

**Interface:** `IReceiptParserService` in `SplitDuo.Core/Domain/Interfaces/`

- Same interface location as other service contracts in the project

**Registration:** `SplitDuo.Api/Extensions/ApiProgramExtensions.cs`

- Scoped, alongside other feature services
- `HttpClient` registered via `AddHttpClient<IReceiptParserService, ReceiptParserService>()`

---

### 4. Controllers — `SplitDuo.Api/Features/`

**New folder:** `SplitDuo.Api/Features/Receipts/Controllers/`

**New file:** `ReceiptsController.cs`

- Inherits `BaseApiController`
- `[Authorize]` — authenticated users only
- `POST /api/v1/receipts/parse` — accepts `IFormFile image`, calls `IReceiptParserService`, returns `ApiResponseDto<ParsedReceiptDto>`
- Returns 503 if `AiOptions.IsEnabled` is false
- Returns 502 if service returns failure

**New folder:** `SplitDuo.Api/Features/Ai/Controllers/`

**New file:** `AiController.cs`

- Inherits `BaseApiController`
- `[Authorize]` — authenticated users only
- `GET /api/v1/ai/status` — reads `IOptions<AiOptions>`, returns `ApiResponseDto<{ bool Enabled }>`

---

## Frontend (`sd-frontend/`)

### 5. Composable — `app/composables/resources/`

**New file:** `useAiStatus.js`

- Singleton pattern — global `ref` declared outside composable function, same as `useCategories.js` and `usePaymentModes.js`
- State: `isAiEnabled` (bool), `isLoading` (bool), `isInitialized` (bool)
- `fetchAiStatus()` — calls `GET /api/v1/ai/status`, no-ops if already initialized
- Exported from barrel: `app/composables/index.js`

---

### 6. Layout — `app/layouts/default.vue`

**Modify:** call `fetchAiStatus()` in setup

- Single line addition — no structural change
- Guarantees fetch runs once on first authenticated page, before any scan button renders

---

### 7. Composable — `app/composables/resources/`

**New file:** `useReceiptScan.js`

- Handles the full scan flow: image compression → API call → navigate
- Image compression via Canvas API (max 2000px, JPEG 80%)
- Calls `POST /api/v1/receipts/parse`
- On success: `router.push('/expenses/add', { query: { ... } })`
- On error: `useNotifications().showError(...)`, no navigation
- Exported from barrel: `app/composables/index.js`

---

### 8. Component — `app/components/ui/`

**New file:** `UiScanReceiptButton.vue`

- Renders only if `isAiEnabled` is true
- Hidden `<input type="file" accept="image/*" capture="environment">` triggered by button click
- On file selected: calls `useReceiptScan` scan method
- Shows loading state during API call
- Accepts `groupId` prop (optional) — passed through to route query on navigate
- Reusable across placement locations

---

### 9. Existing pages — modifications only

**`app/components/groups/ExpensesTab.vue`**

- Add `<UiScanReceiptButton :group-id="groupId" />` near existing "Add Expense" button
- No other changes

**`app/pages/dashboard.vue`** (or equivalent dashboard page)

- Add `<UiScanReceiptButton />` in quick-actions area
- No `groupId` — user selects group in the expense form

**`app/pages/expenses/add.vue`**

- Read route query params on mount: `title`, `amount`, `description`, `expenseDate`, `categoryId`, `paymentModeId`, `groupId`
- Populate form model before first render
- No changes to `ExpenseForm.vue` itself

---

## Summary Table

| Piece | Location | Type |
|---|---|---|
| `AiOptions` | `SplitDuo.Core/Options/AiOptions.cs` | New file |
| `IReceiptParserService` | `SplitDuo.Core/Domain/Interfaces/` | New file |
| `ReceiptParserService` | `SplitDuo.Core/Services/` | New file |
| `ParsedReceiptDto` | `SplitDuo.Api/Features/Receipts/Dto/` | New file |
| `ReceiptsController` | `SplitDuo.Api/Features/Receipts/Controllers/` | New file |
| `AiController` | `SplitDuo.Api/Features/Ai/Controllers/` | New file |
| DI registration | `SplitDuo.Api/Extensions/ApiProgramExtensions.cs` | Modify |
| `useAiStatus.js` | `sd-frontend/app/composables/resources/` | New file |
| `useReceiptScan.js` | `sd-frontend/app/composables/resources/` | New file |
| `UiScanReceiptButton.vue` | `sd-frontend/app/components/ui/` | New file |
| `default.vue` | `sd-frontend/app/layouts/` | Modify (1 line) |
| `ExpensesTab.vue` | `sd-frontend/app/components/groups/` | Modify |
| `dashboard.vue` | `sd-frontend/app/pages/` | Modify |
| `expenses/add.vue` | `sd-frontend/app/pages/expenses/` | Modify |
| Barrel export | `sd-frontend/app/composables/index.js` | Modify |
