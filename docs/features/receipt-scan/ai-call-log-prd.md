# AI Call Log — PRD

## Goal

Track every AI call made by `ReceiptParserService` in a DB table. Enables debugging, usage monitoring, and cost estimation.

---

## Entity: `AiCallLog`

Plain entity — no `AuditableEntity` base, no Guid, no soft-delete.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `int` | No | PK, auto-increment |
| `requested_at` | `long` | No | Unix seconds — before AI call |
| `responded_at` | `long` | Yes | Unix seconds — after AI response; null on failure |
| `input_tokens` | `int` | Yes | From `response.Value.Usage.InputTokenCount` |
| `output_tokens` | `int` | Yes | From `response.Value.Usage.OutputTokenCount` |
| `total_tokens` | `int` | Yes | From `response.Value.Usage.TotalTokenCount` |
| `model` | `string` | No | `AiOptions.Model` at call time |
| `success` | `bool` | No | True if JSON parsed successfully |
| `error_message` | `string` | Yes | Exception message or parse failure reason |
| `user_id` | `int` | No | FK → `Users.Id` — caller identity |

---

## Implementation

### Why service saves (not controller)

The "controller saves" rule applies to business logic. This is audit logging — infrastructure concern. Log must persist on both success **and** failure. Controller only saves on `result.IsSuccess`, so controller-side logging would silently drop failure records.

Precedent: `AuditSaveChangesInterceptor` auto-saves timestamps without controller involvement — same rationale.

### Flow

```
ReceiptParserService.ParseReceiptAsync(imageStream)
  requestedAt = now
  try
    call AI
    respondedAt = now
    extract usage
    parse JSON → dto
    log(success=true, tokens, respondedAt)
    SaveChangesAsync()           ← dedicated save for log only
    return Result.Success(dto)
  catch
    log(success=false, errorMessage, respondedAt=now)
    SaveChangesAsync()           ← still saves on failure
    return Result.BadRequest(...)
```

`SaveChangesAsync()` called inside the service is scoped to the log row only — no open business transaction at this point.

---

## Files

| File | Change |
|---|---|
| `SplitDuo.Core/Domain/Entities/AiCallLog.cs` | New entity |
| `SplitDuo.Core/Persistence/AppContext.cs` | Add `DbSet<AiCallLog>` |
| `SplitDuo.Core/Persistence/UnitOfWork.cs` | Add to interface + impl |
| `SplitDuo.Core/Services/ReceiptParserService.cs` | Inject `IUnitOfWork` + `IUserContextService`, record + save log |
| EF migration | `AddAiCallLog` |

---

## Out of Scope

- Admin UI to query logs
- Log pruning / retention policy
- Alerting on failure rate
