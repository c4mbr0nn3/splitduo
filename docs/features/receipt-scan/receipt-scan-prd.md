# PRD: Receipt Scan

## Summary

Receipt Scan is an opt-in feature that lets users photograph or upload a receipt and have AI extract the relevant expense data, pre-filling the expense creation form for review before saving. The feature is invisible unless the administrator has configured an AI backend via environment variables.

---

## Motivation

Manually entering expenses from receipts is repetitive and error-prone. Users must read amounts, dates, and merchant names from a physical receipt and type them into a form — a flow that is especially tedious on mobile. Receipt Scan removes this friction by letting the user point their camera at a receipt and get a pre-filled form in return. The user stays in control: the form is always shown for review before anything is saved.

The feature is designed to be self-hosted friendly. It works with any OpenAI-compatible vision API — including local Ollama instances — so users are not forced into a specific cloud provider or recurring AI cost.

---

## User Stories

### US-1 — Scan a receipt from the expense list

**As a** user viewing a group's expense list,  
**I want to** scan a receipt directly from that page,  
**so that** I can create a new expense without navigating away first.

### US-2 — Scan a receipt from the dashboard

**As a** user on the dashboard,  
**I want to** scan a receipt as a quick action,  
**so that** I can log an expense immediately without first selecting a group.

### US-3 — Pre-filled form review

**As a** user who has scanned a receipt,  
**I want to** see the expense form pre-filled with the extracted data,  
**so that** I can verify, correct if needed, and submit with minimal effort.

### US-4 — Graceful scan failure

**As a** user whose scan failed (bad image, misconfigured AI, network error),  
**I want to** see a clear error message and stay on my current page,  
**so that** I can try again or fall back to manual entry without losing my context.

### US-5 — Feature hidden when not configured

**As an** administrator who has not configured an AI backend,  
**I want** the scan UI to be completely absent,  
**so that** users are never shown a feature that cannot work.

### US-6 — Feature visible when configured

**As an** administrator who has configured an AI backend,  
**I want** the scan buttons to appear automatically,  
**so that** no frontend deployment or code change is needed to activate the feature.

---

## Acceptance Criteria

### US-1 — Scan from expense list

**Given** the AI module is enabled and the user is viewing a group's expense list  
**When** the page loads  
**Then** a "Scan Receipt" button is visible near the "Add Expense" button

**Given** the user taps "Scan Receipt"  
**When** the file picker opens  
**Then** it accepts any image file and on mobile defaults to the rear camera

**Given** the user selects an image  
**When** the image is selected  
**Then** the image is compressed to max 2000px on the longest side at 80% JPEG quality before upload

**Given** the image is compressed  
**When** the upload and AI processing complete successfully  
**Then** the user is navigated to `/expenses/add` with the group pre-selected and all extractable fields pre-filled

---

### US-2 — Scan from dashboard

**Given** the AI module is enabled and the user is on the dashboard  
**When** the page loads  
**Then** a "Scan Receipt" button is visible in the quick-actions area

**Given** the user taps "Scan Receipt" from the dashboard  
**When** the scan completes successfully  
**Then** the user is navigated to `/expenses/add` without a pre-selected group — the user selects the group in the form

---

### US-3 — Pre-filled form review

**Given** the scan returned a successful response  
**When** the expense form renders  
**Then** `title`, `amount`, `description`, and `expenseDate` are populated from the AI response

**Given** the AI identified a category  
**When** the expense form renders  
**Then** the category selector is pre-selected with the matching `ExpenseCategory` enum value

**Given** the AI could not identify a category  
**When** the expense form renders  
**Then** the category selector is left blank

**Given** the AI identified a payment method  
**When** the expense form renders  
**Then** the payment mode selector is pre-selected with the matching `PaymentMode` enum value

**Given** the AI could not identify a payment method  
**When** the expense form renders  
**Then** the payment mode selector is left blank

**Given** the form is pre-filled  
**When** the user edits any field  
**Then** the form behaves exactly as a manually entered expense — no field is locked or read-only

**Given** the form is pre-filled  
**When** the user submits without changes  
**Then** the expense is created with the AI-extracted values

---

### US-4 — Graceful scan failure

**Given** the AI call fails for any reason (network error, bad model response, timeout)  
**When** the error is returned  
**Then** a toast error message is shown

**Given** a toast error is shown  
**When** the error is displayed  
**Then** the user remains on the page where they tapped "Scan Receipt" — no navigation occurs

**Given** the AI module is enabled but misconfigured (wrong URL, wrong model name)  
**When** the user attempts a scan  
**Then** the backend returns an error and the frontend shows a toast — the form is not opened

---

### US-5 — Feature hidden when not configured

**Given** `SD_AI_BASE_URL` is not set in the environment  
**When** any authenticated user loads any page  
**Then** no "Scan Receipt" button appears anywhere in the UI

**Given** the AI module is disabled  
**When** `POST /api/v1/receipts/parse` is called directly  
**Then** the backend returns HTTP 503

**Given** the AI module is disabled  
**When** `GET /api/v1/ai/status` is called  
**Then** the response is `{ "enabled": false }`

---

### US-6 — Feature visible when configured

**Given** `SD_AI_BASE_URL` and `SD_AI_MODEL` are set in the environment  
**When** an authenticated user loads any page using the default layout  
**Then** `GET /api/v1/ai/status` is called once per session

**Given** `GET /api/v1/ai/status` returns `{ "enabled": true }`  
**When** the user visits the expense list or dashboard  
**Then** the "Scan Receipt" button is rendered

**Given** the status has been fetched once  
**When** the user navigates to another page  
**Then** no additional status API call is made — the cached result is used

---

## Out of Scope (v1)

- Multi-item receipt → multiple expenses
- Currency conversion (EUR only; printed amount used as-is)
- Receipt image storage or history
- Confidence scores per extracted field
- Admin UI for AI configuration (env vars only)
