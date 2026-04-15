# Receipt Scan Feature

## Overview

Opt-in feature that allows users to photograph or upload a receipt and use a vision AI model to extract expense data, pre-filling the expense creation form for review before saving.

Feature is hidden in the UI if the backend AI module is not configured.

---

## Configuration

Three environment variables control the feature. If `SD_AI_BASE_URL` is absent, the module is disabled and no AI-related UI is rendered.

| Variable | Required | Example | Notes |
|---|---|---|---|
| `SD_AI_BASE_URL` | Yes (to enable) | `http://localhost:11434/v1` | OpenAI-compatible base URL. Ollama: append `/v1`. |
| `SD_AI_API_KEY` | No | `sk-...` | Empty string for Ollama (no auth). Required for cloud APIs (Nebius, OpenAI, etc.). |
| `SD_AI_MODEL` | Yes (if enabled) | `llava:13b` / `llama3.2-vision` | Vision-capable model name passed to the API. |

Backend loads these at startup. If `SD_AI_BASE_URL` is missing, `AiSettings.IsEnabled` is `false` and the parse endpoint returns 503. No startup validation of the URL or model — errors surface at request time.

---

## Backend

### AI Settings

`AiSettings` class bound from environment:

```csharp
public class AiSettings
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public bool IsEnabled => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);
}
```

### Endpoints

#### `GET /api/v1/ai/status`

Returns whether the AI module is enabled. Called by the frontend on startup to conditionally render scan buttons.

**Response:**
```json
{
  "success": true,
  "data": { "enabled": true }
}
```

No auth required — this is a feature flag, not sensitive data.

#### `POST /api/v1/receipts/parse`

Accepts a receipt image, sends it to the configured vision model, returns structured expense data.

**Request:** `multipart/form-data`

| Field | Type | Notes |
|---|---|---|
| `image` | file | JPEG, compressed client-side before upload |

**Response (success):**
```json
{
  "success": true,
  "data": {
    "title": "Carrefour Market",
    "amount": 34.80,
    "description": "Grocery shopping",
    "expenseDate": "2024-01-15",
    "suggestedCategoryName": "Groceries",
    "suggestedPaymentMethodName": "Credit Card"
  }
}
```

**Response (AI module disabled):**
```json
{ "success": false, "error": "AI module is not configured." }
```
HTTP 503.

**Response (AI call fails / bad response):**
```json
{ "success": false, "error": "Failed to parse receipt. Check AI configuration." }
```
HTTP 502.

### AI Prompt

Sent as a user message with the image attached (base64 inline, `image/jpeg`):

```
You are a receipt parser. Extract the following fields from the receipt image and return a JSON object. Return ONLY the JSON object, no explanation, no markdown.

{
  "title": "merchant name or store name",
  "amount": 0.00,
  "description": "brief summary of items or purchase type, or null",
  "expenseDate": "YYYY-MM-DD",
  "suggestedCategoryName": "one of: Groceries, Restaurant, Transport, Entertainment, Health, Shopping, Utilities, Travel, Other",
  "suggestedPaymentMethodName": "one of: Cash, Credit Card, Debit Card, or null if not visible"
}

Rules:
- amount is the total amount paid, as a number (no currency symbol)
- expenseDate must be in YYYY-MM-DD format; use today if not visible
- suggestedCategoryName must be one of the listed values; pick the closest match
- suggestedPaymentMethodName must be one of the listed values or null
- Return null for fields you cannot determine
```

Backend parses the response by extracting the first `{...}` block from the model output (models may prepend/append text despite instructions). If JSON parsing fails, return 502.

### ReceiptParserService

New service in `SplitDuo.Core`:

- Injected `AiSettings` and `HttpClient`
- `ParseReceiptAsync(Stream imageStream)` → `Result<ParsedReceiptDto>`
- Builds the OpenAI-compatible chat completion request with vision content
- Extracts and deserializes JSON from model response
- Returns `Result.Failure(...)` on HTTP error, timeout, or JSON parse failure

---

## Frontend

### AI Status

New composable `useAiStatus` (singleton, fetches once on first use):

```javascript
const { isAiEnabled } = useAiStatus()
```

Used to conditionally render scan buttons across the app.

### Image Compression

Client-side before upload, using Canvas API:

- Resize to max 2000px on longest side (preserve aspect ratio)
- Re-encode as JPEG at 80% quality
- Result uploaded as `image/jpeg`

### UX Flow

1. User taps **Scan Receipt** button (visible only if `isAiEnabled`)
2. Native file picker opens (`accept="image/*"`, `capture="environment"` on mobile for camera)
3. Selected image compressed client-side
4. Loading overlay shown ("Scanning receipt...")
5. `POST /api/v1/receipts/parse` called with compressed image
6. **On success**: navigate to `/expenses/add` with parsed data pre-filled in the form
   - `title`, `amount`, `description`, `expenseDate` populated directly
   - `categoryId`: frontend iterates loaded categories, picks first case-insensitive name match on `suggestedCategoryName`; leaves blank if no match
   - `paymentModeId`: same fuzzy match on `suggestedPaymentMethodName`; leaves blank if no match
7. **On error**: show toast error, stay on current page (no navigation)

### Scan Button Placement

Two locations:

- **Expense list page** (`/groups/[id]` → ExpensesTab): "Scan Receipt" button near the existing "Add Expense" button
- **Dashboard / index page**: "Scan Receipt" shortcut alongside other quick-action buttons

Button is not embedded inside `ExpenseForm`. The form receives pre-filled model data via route query params or navigation state — same mechanism regardless of scan or manual entry.

### Data Passing to ExpenseForm

Parsed data passed as route query on navigation to `/expenses/add`:

```javascript
router.push({
  path: '/expenses/add',
  query: {
    groupId: currentGroupId,   // pre-select group from context
    title: parsed.title,
    amount: parsed.amount,
    description: parsed.description,
    expenseDate: parsed.expenseDate,
    suggestedCategoryName: parsed.suggestedCategoryName,
    suggestedPaymentMethodName: parsed.suggestedPaymentMethodName,
  }
})
```

The add-expense page reads query params on mount and populates the form model before rendering.

---

## Currency

Currency is fixed to EUR. The AI response does not include a currency field. If the receipt shows a different currency, the amount extracted is the printed total — no conversion.

---

## Sequence

```
User taps "Scan Receipt"
  → file picker (camera or gallery)
  → client compresses image (max 2000px, JPEG 80%)
  → POST /api/v1/receipts/parse (multipart)
      → backend sends to AI (OpenAI-compatible /v1/chat/completions)
      → AI returns JSON
      → backend parses, returns ParsedReceiptDto
  → frontend navigates to /expenses/add with pre-filled query params
  → user reviews form, edits if needed, submits
```

---

## Out of Scope (v1)

- Multi-item receipt → multiple expenses
- Currency conversion
- Receipt image storage / history
- Confidence scores per field
- Admin UI for AI configuration (env vars only)
