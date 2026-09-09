using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.RecurringExpenses.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Expenses;
using SplitDuo.Core.Services.Recurring;

namespace SplitDuo.Api.Features.RecurringExpenses.Services;

public interface IRecurringExpensesService
{
    Task<Result<List<RecurringExpenseTemplateDto>>> GetTemplatesAsync(
        string groupId, Guid currentUserId, bool includeInactive);

    Task<Result<RecurringExpenseTemplateDto>> GetTemplateAsync(
        string groupId, string templateId, Guid currentUserId);

    Task<Result<RecurringExpenseTemplateDto>> CreateTemplateAsync(
        string groupId, CreateRecurringExpenseTemplateDto request, Guid currentUserId);

    Task<Result<RecurringExpenseTemplateDto>> UpdateTemplateAsync(
        string groupId, string templateId, UpdateRecurringExpenseTemplateDto request, Guid currentUserId);

    Task<Result> DeleteTemplateAsync(string groupId, string templateId, Guid currentUserId);

    Task<Result<RecurringExpenseTemplateDto>> ToggleActiveAsync(
        string groupId, string templateId, ToggleActiveRequestDto request, Guid currentUserId);

    Task<Result<RecurringExpenseTemplateDto>> ResumeAsync(
        string groupId, string templateId, ResumeRecurringExpenseTemplateDto request, Guid currentUserId);

    Task<Result<PaginatedResponseDto<RecurringExpenseInstanceDto>>> GetInstancesAsync(
        string groupId, string? templateId, string? status, int page, int limit, Guid currentUserId);

    Task<Result<ExpenseDto>> ApproveInstanceAsync(
        string groupId, string instanceId, ApproveRecurringExpenseInstanceDto? request, Guid currentUserId);

    Task<Result> RejectInstanceAsync(string groupId, string instanceId, Guid currentUserId);

    Task<Result<RecurrencePreviewResponseDto>> PreviewOccurrencesAsync(
        string groupId, RecurrencePreviewRequestDto request, Guid currentUserId);
}

public class RecurringExpensesService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IExpenseCreationService expenseCreation,
    IStringLocalizer<RecurringExpensesService> loc,
    IStringLocalizer<RecurrenceEvaluator> describeLoc) : IRecurringExpensesService
{
    public async Task<Result<List<RecurringExpenseTemplateDto>>> GetTemplatesAsync(
        string groupId, Guid currentUserId, bool includeInactive)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<List<RecurringExpenseTemplateDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<List<RecurringExpenseTemplateDto>>();
        var (group, _, _) = memberResult.Value!;

        var query = unitOfWork.RecurringExpenseTemplates
            .Where(t => t.GroupId == group.Id && t.DeletedAt == null)
            .Include(t => t.Group)
            .Include(t => t.Owner)
            .Include(t => t.PaidByUser)
            .Include(t => t.PaidByAlias)
            .Include(t => t.Splits).ThenInclude(s => s.User)
            .Include(t => t.AliasSplits).ThenInclude(s => s.Alias)
            .AsSplitQuery()
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        var templates = await query
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var pendingCountByTemplate = await unitOfWork.RecurringExpenseInstances
            .Where(i => i.StatusId == (int)RecurringExpenseInstanceStatus.PendingApproval &&
                        i.DeletedAt == null &&
                        i.Template.GroupId == group.Id)
            .GroupBy(i => i.TemplateId)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Count);

        var dtos = (await Task.WhenAll(templates
                .Select(t => MapTemplate(t, pendingCountByTemplate.GetValueOrDefault(t.Id)))))
            .ToList();

        return Result<List<RecurringExpenseTemplateDto>>.Success(dtos);
    }

    public async Task<Result<RecurringExpenseTemplateDto>> GetTemplateAsync(
        string groupId, string templateId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(templateId, out var templateGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidTemplateIdFormat"]);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<RecurringExpenseTemplateDto>();

        var template = await unitOfWork.RecurringExpenseTemplates
            .Include(t => t.Group)
            .Include(t => t.Owner)
            .Include(t => t.PaidByUser)
            .Include(t => t.PaidByAlias)
            .Include(t => t.Splits).ThenInclude(s => s.User)
            .Include(t => t.AliasSplits).ThenInclude(s => s.Alias)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Guid == templateGuid && t.GroupId == memberResult.Value!.Group.Id && t.DeletedAt == null);

        if (template == null)
            return Result<RecurringExpenseTemplateDto>.NotFound(loc["TemplateNotFound"]);

        var pendingCount = await unitOfWork.RecurringExpenseInstances
            .CountAsync(i => i.TemplateId == template.Id &&
                             i.StatusId == (int)RecurringExpenseInstanceStatus.PendingApproval &&
                             i.DeletedAt == null);

        return Result<RecurringExpenseTemplateDto>.Success(await MapTemplate(template, pendingCount));
    }

    public async Task<Result<RecurringExpenseTemplateDto>> CreateTemplateAsync(
        string groupId, CreateRecurringExpenseTemplateDto request, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(request.PaidByUserId, out var paidByUserGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidPaidByUserIdFormat"]);

        if (!DateOnly.TryParse(request.AnchorDate, out var anchorDate))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidDateFormat"]);

        DateOnly? endDate = null;
        if (!string.IsNullOrWhiteSpace(request.EndDate))
        {
            if (!DateOnly.TryParse(request.EndDate, out var parsedEndDate))
                return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidDateFormat"]);

            if (parsedEndDate < anchorDate)
                return Result<RecurringExpenseTemplateDto>.BadRequest(loc["EndDateBeforeAnchor"]);

            endDate = parsedEndDate;
        }

        if (!Enum.IsDefined(typeof(RecurrenceMode), request.RecurrenceMode))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidRecurrenceMode"]);

        if (!Enum.IsDefined(typeof(ExpenseCategory), request.CategoryId))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidExpenseCategory"]);

        if (!Enum.IsDefined(typeof(PaymentMode), request.PaymentModeId))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidExpensePaymentMode"]);

        var specResult = ValidateSpec(
            (RecurrenceMode)request.RecurrenceMode, request.Weekdays, request.DayOfMonth, request.Interval);
        if (specResult.IsFailure)
            return Result<RecurringExpenseTemplateDto>.BadRequest(specResult.Error);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<RecurringExpenseTemplateDto>();
        var (group, currentUser, _) = memberResult.Value!;

        var paidByUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == paidByUserGuid && u.DeletedAt == null);

        if (paidByUser == null)
            return Result<RecurringExpenseTemplateDto>.NotFound(loc["PaidByUserNotFound"]);

        var isPaidByUserMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

        if (!isPaidByUserMember)
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["PaidByUserNotMember"]);

        var payerAliasResult = await ResolvePayerAliasAsync(group, request.PaidByAliasId, paidByUser);
        if (payerAliasResult.IsFailure)
            return payerAliasResult.MapTo<RecurringExpenseTemplateDto>();

        var splitsResult = await ValidateAndBuildSplitsAsync(
            group, request.Amount, request.Splits, request.AliasSplits);
        if (splitsResult.IsFailure)
            return splitsResult.MapTo<RecurringExpenseTemplateDto>();
        var (validatedUsers, validatedAliases) = splitsResult.Value!;

        var template = new RecurringExpenseTemplate
        {
            GroupId = group.Id,
            Group = group,
            OwnerId = currentUser.Id,
            Owner = currentUser,
            Title = request.Title,
            Description = request.Description,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            PaymentModeId = request.PaymentModeId,
            PaidByUserId = paidByUser.Id,
            PaidByUser = paidByUser,
            PaidByAliasId = payerAliasResult.Value,
            RecurrenceModeId = request.RecurrenceMode,
            Weekdays = request.Weekdays,
            DayOfMonth = request.DayOfMonth,
            Interval = request.Interval,
            AnchorDate = anchorDate,
            EndDate = endDate,
            RequiresApproval = request.RequiresApproval,
            IsActive = true
        };

        foreach (var split in request.Splits)
        {
            var splitUser = validatedUsers[Guid.Parse(split.UserId).ToString()];
            template.Splits.Add(new RecurringExpenseTemplateSplit
            {
                UserId = splitUser.Id,
                User = splitUser,
                SplitAmount = split.SplitAmount
            });
        }

        if (request.AliasSplits != null)
        {
            foreach (var split in request.AliasSplits)
            {
                var alias = validatedAliases[Guid.Parse(split.AliasId).ToString()];
                template.AliasSplits.Add(new RecurringExpenseTemplateAliasSplit
                {
                    AliasId = alias.Id,
                    Alias = alias,
                    SplitAmount = split.SplitAmount
                });
            }
        }

        await unitOfWork.RecurringExpenseTemplates.AddAsync(template);

        return Result<RecurringExpenseTemplateDto>.Success(await MapTemplate(template, 0));
    }

    public async Task<Result<RecurringExpenseTemplateDto>> UpdateTemplateAsync(
        string groupId, string templateId, UpdateRecurringExpenseTemplateDto request, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(templateId, out var templateGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidTemplateIdFormat"]);

        if (!Guid.TryParse(request.PaidByUserId, out var paidByUserGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidPaidByUserIdFormat"]);

        if (!DateOnly.TryParse(request.AnchorDate, out var anchorDate))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidDateFormat"]);

        DateOnly? endDate = null;
        if (!string.IsNullOrWhiteSpace(request.EndDate))
        {
            if (!DateOnly.TryParse(request.EndDate, out var parsedEndDate))
                return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidDateFormat"]);

            if (parsedEndDate < anchorDate)
                return Result<RecurringExpenseTemplateDto>.BadRequest(loc["EndDateBeforeAnchor"]);

            endDate = parsedEndDate;
        }

        if (!Enum.IsDefined(typeof(RecurrenceMode), request.RecurrenceMode))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidRecurrenceMode"]);

        if (!Enum.IsDefined(typeof(ExpenseCategory), request.CategoryId))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidExpenseCategory"]);

        if (!Enum.IsDefined(typeof(PaymentMode), request.PaymentModeId))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidExpensePaymentMode"]);

        var specResult = ValidateSpec(
            (RecurrenceMode)request.RecurrenceMode, request.Weekdays, request.DayOfMonth, request.Interval);
        if (specResult.IsFailure)
            return Result<RecurringExpenseTemplateDto>.BadRequest(specResult.Error);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<RecurringExpenseTemplateDto>();
        var (group, currentUser, membership) = memberResult.Value!;

        var template = await unitOfWork.RecurringExpenseTemplates
            .Include(t => t.Group)
            .Include(t => t.Owner)
            .Include(t => t.PaidByUser)
            .Include(t => t.PaidByAlias)
            .Include(t => t.Splits).ThenInclude(s => s.User)
            .Include(t => t.AliasSplits).ThenInclude(s => s.Alias)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Guid == templateGuid && t.GroupId == group.Id && t.DeletedAt == null);

        if (template == null)
            return Result<RecurringExpenseTemplateDto>.NotFound(loc["TemplateNotFound"]);

        // Owner OR group admin only
        if (template.OwnerId != currentUser.Id && membership.Role != GroupRole.Admin)
            return Result<RecurringExpenseTemplateDto>.Forbidden(loc["OnlyOwnerOrAdminCanModify"]);

        var paidByUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == paidByUserGuid && u.DeletedAt == null);

        if (paidByUser == null)
            return Result<RecurringExpenseTemplateDto>.NotFound(loc["PaidByUserNotFound"]);

        var isPaidByUserMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

        if (!isPaidByUserMember)
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["PaidByUserNotMember"]);

        var payerAliasResult = await ResolvePayerAliasAsync(group, request.PaidByAliasId, paidByUser);
        if (payerAliasResult.IsFailure)
            return payerAliasResult.MapTo<RecurringExpenseTemplateDto>();

        var splitsResult = await ValidateAndBuildSplitsAsync(
            group, request.Amount, request.Splits, request.AliasSplits);
        if (splitsResult.IsFailure)
            return splitsResult.MapTo<RecurringExpenseTemplateDto>();
        var (validatedUsers, validatedAliases) = splitsResult.Value!;

        // Replace all editable fields. PausedReason is intentionally left as-is —
        // only ToggleActive reactivation clears it. Past instances are untouched
        // (they are already snapshots); only future occurrences see the new values.
        template.Title = request.Title;
        template.Description = request.Description;
        template.Amount = request.Amount;
        template.CategoryId = request.CategoryId;
        template.PaymentModeId = request.PaymentModeId;
        template.PaidByUserId = paidByUser.Id;
        template.PaidByUser = paidByUser;
        template.PaidByAliasId = payerAliasResult.Value;
        template.RecurrenceModeId = request.RecurrenceMode;
        template.Weekdays = request.Weekdays;
        template.DayOfMonth = request.DayOfMonth;
        template.Interval = request.Interval;
        template.AnchorDate = anchorDate;
        template.EndDate = endDate;
        template.RequiresApproval = request.RequiresApproval;

        // Replace split snapshots
        if (template.Splits.Count > 0)
            unitOfWork.RecurringExpenseTemplateSplits.RemoveRange(template.Splits);

        if (template.AliasSplits.Count > 0)
            unitOfWork.RecurringExpenseTemplateAliasSplits.RemoveRange(template.AliasSplits);

        foreach (var split in request.Splits)
        {
            var splitUser = validatedUsers[Guid.Parse(split.UserId).ToString()];
            template.Splits.Add(new RecurringExpenseTemplateSplit
            {
                UserId = splitUser.Id,
                User = splitUser,
                SplitAmount = split.SplitAmount
            });
        }

        if (request.AliasSplits != null)
        {
            foreach (var split in request.AliasSplits)
            {
                var alias = validatedAliases[Guid.Parse(split.AliasId).ToString()];
                template.AliasSplits.Add(new RecurringExpenseTemplateAliasSplit
                {
                    AliasId = alias.Id,
                    Alias = alias,
                    SplitAmount = split.SplitAmount
                });
            }
        }

        var pendingCount = await unitOfWork.RecurringExpenseInstances
            .CountAsync(i => i.TemplateId == template.Id &&
                             i.StatusId == (int)RecurringExpenseInstanceStatus.PendingApproval &&
                             i.DeletedAt == null);

        return Result<RecurringExpenseTemplateDto>.Success(await MapTemplate(template, pendingCount));
    }

    public async Task<Result> DeleteTemplateAsync(string groupId, string templateId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(templateId, out var templateGuid))
            return Result.BadRequest(loc["InvalidTemplateIdFormat"]);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.ToResult();
        var (group, currentUser, membership) = memberResult.Value!;

        var template = await unitOfWork.RecurringExpenseTemplates
            .FirstOrDefaultAsync(t => t.Guid == templateGuid && t.GroupId == group.Id && t.DeletedAt == null);

        if (template == null)
            return Result.NotFound(loc["TemplateNotFound"]);

        if (template.OwnerId != currentUser.Id && membership.Role != GroupRole.Admin)
            return Result.Forbidden(loc["OnlyOwnerOrAdminCanModify"]);

        // Soft-delete via DeletedAt (mirrors ExpensesService.DeleteExpenseAsync).
        // No instance/expense changes — already-created data is snapshotted.
        template.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        return Result.Success();
    }

    public async Task<Result<RecurringExpenseTemplateDto>> ToggleActiveAsync(
        string groupId, string templateId, ToggleActiveRequestDto request, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(templateId, out var templateGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidTemplateIdFormat"]);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<RecurringExpenseTemplateDto>();
        var (group, currentUser, membership) = memberResult.Value!;

        var template = await unitOfWork.RecurringExpenseTemplates
            .Include(t => t.Group)
            .Include(t => t.Owner)
            .Include(t => t.PaidByUser)
            .Include(t => t.PaidByAlias)
            .Include(t => t.Splits).ThenInclude(s => s.User)
            .Include(t => t.AliasSplits).ThenInclude(s => s.Alias)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Guid == templateGuid && t.GroupId == group.Id && t.DeletedAt == null);

        if (template == null)
            return Result<RecurringExpenseTemplateDto>.NotFound(loc["TemplateNotFound"]);

        if (template.OwnerId != currentUser.Id && membership.Role != GroupRole.Admin)
            return Result<RecurringExpenseTemplateDto>.Forbidden(loc["OnlyOwnerOrAdminCanModify"]);

        template.IsActive = request.IsActive;

        // Only manual toggle reactivation clears PausedReason
        if (request.IsActive)
        {
            template.PausedReason = null;
            // Back-compat default (spec D3/D2): resume-via-toggle applies SKIP semantics —
            // missed periods are permanently excluded. The explicit choice lives on /resume.
            template.ResumeFrom = ComputeSkipBoundary(template);
        }

        var pendingCount = await unitOfWork.RecurringExpenseInstances
            .CountAsync(i => i.TemplateId == template.Id &&
                             i.StatusId == (int)RecurringExpenseInstanceStatus.PendingApproval &&
                             i.DeletedAt == null);

        return Result<RecurringExpenseTemplateDto>.Success(await MapTemplate(template, pendingCount));
    }

    public async Task<Result<RecurringExpenseTemplateDto>> ResumeAsync(
        string groupId, string templateId, ResumeRecurringExpenseTemplateDto request, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(templateId, out var templateGuid))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidTemplateIdFormat"]);

        if (request.Strategy is not ("backfill" or "skip"))
            return Result<RecurringExpenseTemplateDto>.BadRequest(loc["InvalidResumeStrategy"]);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<RecurringExpenseTemplateDto>();
        var (group, currentUser, membership) = memberResult.Value!;

        var template = await unitOfWork.RecurringExpenseTemplates
            .Include(t => t.Group)
            .Include(t => t.Owner)
            .Include(t => t.PaidByUser)
            .Include(t => t.PaidByAlias)
            .Include(t => t.Splits).ThenInclude(s => s.User)
            .Include(t => t.AliasSplits).ThenInclude(s => s.Alias)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Guid == templateGuid && t.GroupId == group.Id && t.DeletedAt == null);

        if (template == null)
            return Result<RecurringExpenseTemplateDto>.NotFound(loc["TemplateNotFound"]);

        if (template.OwnerId != currentUser.Id && membership.Role != GroupRole.Admin)
            return Result<RecurringExpenseTemplateDto>.Forbidden(loc["OnlyOwnerOrAdminCanModify"]);

        template.IsActive = true;
        template.PausedReason = null;

        if (request.Strategy == "skip")
        {
            // Skip: permanently exclude everything up to (but not including) the first
            // occurrence strictly after today. Backfill: leave ResumeFrom unchanged so
            // the job's window covers [AnchorDate, today] and backfills the gap.
            template.ResumeFrom = ComputeSkipBoundary(template);
        }

        var pendingCount = await unitOfWork.RecurringExpenseInstances
            .CountAsync(i => i.TemplateId == template.Id &&
                             i.StatusId == (int)RecurringExpenseInstanceStatus.PendingApproval &&
                             i.DeletedAt == null);

        return Result<RecurringExpenseTemplateDto>.Success(await MapTemplate(template, pendingCount));
    }

    /// <summary>
    /// The first occurrence strictly after today (fake clock), or today+1 when the
    /// schedule has no future occurrence. Shared by ResumeAsync (skip) and
    /// ToggleActiveAsync (back-compat default) so the boundary is computed one way.
    /// </summary>
    private DateOnly ComputeSkipBoundary(RecurringExpenseTemplate template)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var spec = new RecurrenceEvaluator.RecurrenceSpec(
            (RecurrenceMode)template.RecurrenceModeId, template.Weekdays,
            template.DayOfMonth, template.Interval, template.AnchorDate, template.EndDate);

        var windowEnd = template.EndDate.HasValue && template.EndDate.Value < today.AddYears(10)
            ? template.EndDate.Value
            : today.AddYears(10);

        return RecurrenceEvaluator.GetOccurrences(spec, today.AddDays(1), windowEnd)
                   .Cast<DateOnly?>()
                   .FirstOrDefault()
               ?? today.AddDays(1);
    }

    public async Task<Result<PaginatedResponseDto<RecurringExpenseInstanceDto>>> GetInstancesAsync(
        string groupId, string? templateId, string? status, int page, int limit, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<PaginatedResponseDto<RecurringExpenseInstanceDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<PaginatedResponseDto<RecurringExpenseInstanceDto>>();
        var (group, _, _) = memberResult.Value!;

        var query = unitOfWork.RecurringExpenseInstances
            .Where(i => i.Template.GroupId == group.Id && i.DeletedAt == null && i.Template.DeletedAt == null)
            .Include(i => i.Template)
            .Include(i => i.ApprovedBy)
            .Include(i => i.Expense)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(templateId))
        {
            if (!Guid.TryParse(templateId, out var templateGuid))
                return Result<PaginatedResponseDto<RecurringExpenseInstanceDto>>.BadRequest(loc["InvalidTemplateIdFormat"]);

            var template = await unitOfWork.RecurringExpenseTemplates
                .FirstOrDefaultAsync(t => t.Guid == templateGuid && t.GroupId == group.Id && t.DeletedAt == null);

            if (template == null)
                return Result<PaginatedResponseDto<RecurringExpenseInstanceDto>>.NotFound(loc["TemplateNotFound"]);

            query = query.Where(i => i.TemplateId == template.Id);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RecurringExpenseInstanceStatus>(status, true, out var statusEnum) ||
                !Enum.IsDefined(statusEnum))
                return Result<PaginatedResponseDto<RecurringExpenseInstanceDto>>.BadRequest(loc["InvalidInstanceStatus"]);

            query = query.Where(i => i.StatusId == (int)statusEnum);
        }

        var totalCount = await query.CountAsync();

        var instances = await query
            .OrderByDescending(i => i.PeriodDate)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        // Load draft splits for this page of instances (batch, avoids N+1)
        var instanceIds = instances.Select(i => i.Id).ToList();

        var splitsByInstance = await unitOfWork.RecurringExpenseInstanceSplits
            .Where(s => instanceIds.Contains(s.InstanceId))
            .Include(s => s.User)
            .ToListAsync();

        Dictionary<int, List<RecurringExpenseInstanceAliasSplit>>? aliasSplitsByInstance = null;
        if (group.UseAliases)
        {
            aliasSplitsByInstance = (await unitOfWork.RecurringExpenseInstanceAliasSplits
                    .Where(s => instanceIds.Contains(s.InstanceId))
                    .Include(s => s.Alias)
                    .ToListAsync())
                .GroupBy(s => s.InstanceId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        var dtos = instances.Select(instance =>
        {
            var dto = new RecurringExpenseInstanceDto(instance);

            dto.Splits = splitsByInstance
                .Where(s => s.InstanceId == instance.Id)
                .Select(s => new RecurringExpenseSplitDto
                {
                    UserId = s.User.Guid.ToString(),
                    SplitAmount = s.SplitAmount
                })
                .ToList();

            if (aliasSplitsByInstance != null &&
                aliasSplitsByInstance.TryGetValue(instance.Id, out var instanceAliasSplits))
            {
                dto.AliasSplits = instanceAliasSplits
                    .Select(s => new RecurringExpenseAliasSplitDto
                    {
                        AliasId = s.Alias.Guid.ToString(),
                        SplitAmount = s.SplitAmount
                    })
                    .ToList();
            }

            return dto;
        }).ToList();

        var pagination = new PaginationDto
        {
            Page = page,
            Limit = limit,
            Total = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / limit),
            HasNext = page * limit < totalCount,
            HasPrev = page > 1
        };

        var response = PaginatedResponseDto<RecurringExpenseInstanceDto>.SuccessResponse(dtos, pagination);
        return Result<PaginatedResponseDto<RecurringExpenseInstanceDto>>.Success(response);
    }

    public async Task<Result<ExpenseDto>> ApproveInstanceAsync(
        string groupId, string instanceId, ApproveRecurringExpenseInstanceDto? request, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<ExpenseDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(instanceId, out var instanceGuid))
            return Result<ExpenseDto>.BadRequest(loc["InvalidInstanceIdFormat"]);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<ExpenseDto>();
        var (group, currentUser, membership) = memberResult.Value!;

        var instance = await unitOfWork.RecurringExpenseInstances
            .Include(i => i.Template)
            .Include(i => i.Expense)
            .FirstOrDefaultAsync(i => i.Guid == instanceGuid && i.DeletedAt == null &&
                                      i.Template.GroupId == group.Id && i.Template.DeletedAt == null);

        if (instance == null)
            return Result<ExpenseDto>.NotFound(loc["InstanceNotFound"]);

        var template = instance.Template;

        if (template.OwnerId != currentUser.Id && membership.Role != GroupRole.Admin)
            return Result<ExpenseDto>.Forbidden(loc["OnlyOwnerOrAdminCanModify"]);

        if (instance.StatusId != (int)RecurringExpenseInstanceStatus.PendingApproval)
            return Result<ExpenseDto>.Conflict(loc["InstanceNotPendingApproval"]);

        // Overrides: instance draft payload is the default; dto values win where provided
        var amount = request?.Amount ?? instance.Amount;

        var expenseDate = instance.PeriodDate;
        if (!string.IsNullOrWhiteSpace(request?.ExpenseDate))
        {
            if (!DateOnly.TryParse(request.ExpenseDate, out var parsedDate))
                return Result<ExpenseDto>.BadRequest(loc["InvalidDateFormat"]);
            expenseDate = parsedDate;
        }

        List<ExpenseSplitInput>? splitsInput;
        List<ExpenseAliasSplitInput>? aliasSplitsInput;

        if (request?.Splits is { Count: > 0 })
        {
            splitsInput = request.Splits
                .Select(s => new ExpenseSplitInput(s.UserId, s.SplitAmount))
                .ToList();
            aliasSplitsInput = null;
        }
        else if (request?.AliasSplits is { Count: > 0 })
        {
            aliasSplitsInput = request.AliasSplits!
                .Select(s => new ExpenseAliasSplitInput(s.AliasId, s.SplitAmount))
                .ToList();
            splitsInput = null;
        }
        else
        {
            // Fall back to the draft split snapshot stored on the instance
            var draftSplits = await unitOfWork.RecurringExpenseInstanceSplits
                .Where(s => s.InstanceId == instance.Id)
                .Include(s => s.User)
                .ToListAsync();

            var draftAliasSplits = await unitOfWork.RecurringExpenseInstanceAliasSplits
                .Where(s => s.InstanceId == instance.Id)
                .Include(s => s.Alias)
                .ToListAsync();

            if (draftAliasSplits.Count > 0)
            {
                aliasSplitsInput = draftAliasSplits
                    .Select(s => new ExpenseAliasSplitInput(s.Alias.Guid.ToString(), s.SplitAmount))
                    .ToList();
                splitsInput = null;
            }
            else
            {
                splitsInput = draftSplits
                    .Select(s => new ExpenseSplitInput(s.User.Guid.ToString(), s.SplitAmount))
                    .ToList();
                aliasSplitsInput = null;
            }
        }

        var paidByUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Id == template.PaidByUserId && u.DeletedAt == null);

        if (paidByUser == null)
            return Result<ExpenseDto>.NotFound(loc["PaidByUserNotFound"]);

        // Delegate validation + entity creation to the shared Core service. The
        // Result is passed through unchanged; the controller owns SaveChanges.
        var creationResult = await expenseCreation.CreateValidatedExpenseAsync(
            group, paidByUser, expenseDate,
            template.Title, template.Description, amount,
            (ExpenseCategory)template.CategoryId, (PaymentMode)template.PaymentModeId,
            splitsInput, aliasSplitsInput,
            recurringExpenseTemplateId: template.Id);

        if (creationResult.IsFailure)
            return creationResult.MapTo<ExpenseDto>();

        var expense = creationResult.Value!;

        instance.StatusId = (int)RecurringExpenseInstanceStatus.Approved;
        instance.ApprovedById = currentUser.Id;
        instance.ApprovedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        // Set the navigation (not ExpenseId) — the expense is still Added/unsaved with
        // Id == 0; assigning the navigation lets EF fix up the FK after it gets its
        // store-generated id during SaveChanges. Assigning ExpenseId = 0 breaks the save.
        instance.Expense = expense;

        // Build the ExpenseDto exactly like ExpensesService.CreateExpenseAsync success tail
        var expenseDto = group.UseAliases
            ? new ExpenseDto(expense, null, expense.ExpenseAliasSplits.ToList())
            : new ExpenseDto(expense, expense.ExpenseSplits.ToList());

        await SetHasAvatarAsync(expenseDto, expense, group.UseAliases ? null : expense.ExpenseSplits.ToList());

        return Result<ExpenseDto>.Success(expenseDto);
    }

    public async Task<Result> RejectInstanceAsync(string groupId, string instanceId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(instanceId, out var instanceGuid))
            return Result.BadRequest(loc["InvalidInstanceIdFormat"]);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.ToResult();
        var (group, currentUser, membership) = memberResult.Value!;

        var instance = await unitOfWork.RecurringExpenseInstances
            .Include(i => i.Template)
            .FirstOrDefaultAsync(i => i.Guid == instanceGuid && i.DeletedAt == null &&
                                      i.Template.GroupId == group.Id && i.Template.DeletedAt == null);

        if (instance == null)
            return Result.NotFound(loc["InstanceNotFound"]);

        if (instance.Template.OwnerId != currentUser.Id && membership.Role != GroupRole.Admin)
            return Result.Forbidden(loc["OnlyOwnerOrAdminCanModify"]);

        if (instance.StatusId != (int)RecurringExpenseInstanceStatus.PendingApproval)
            return Result.Conflict(loc["InstanceNotPendingApproval"]);

        instance.StatusId = (int)RecurringExpenseInstanceStatus.Rejected;

        // No expense is created; no cache invalidation is needed (no group data changed).
        return Result.Success();
    }

    public async Task<Result<RecurrencePreviewResponseDto>> PreviewOccurrencesAsync(
        string groupId, RecurrencePreviewRequestDto request, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<RecurrencePreviewResponseDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Enum.IsDefined(typeof(RecurrenceMode), request.RecurrenceMode))
            return Result<RecurrencePreviewResponseDto>.BadRequest(loc["InvalidRecurrenceMode"]);

        if (!DateOnly.TryParse(request.AnchorDate, out var anchorDate))
            return Result<RecurrencePreviewResponseDto>.BadRequest(loc["InvalidDateFormat"]);

        DateOnly? endDate = null;
        if (!string.IsNullOrWhiteSpace(request.EndDate))
        {
            if (!DateOnly.TryParse(request.EndDate, out var parsedEndDate))
                return Result<RecurrencePreviewResponseDto>.BadRequest(loc["InvalidDateFormat"]);

            if (parsedEndDate < anchorDate)
                return Result<RecurrencePreviewResponseDto>.BadRequest(loc["EndDateBeforeAnchor"]);

            endDate = parsedEndDate;
        }

        var specResult = ValidateSpec(
            (RecurrenceMode)request.RecurrenceMode, request.Weekdays, request.DayOfMonth, request.Interval);
        if (specResult.IsFailure)
            return Result<RecurrencePreviewResponseDto>.BadRequest(specResult.Error);

        var memberResult = await GetMemberContextAsync(groupGuid, currentUserId);
        if (memberResult.IsFailure)
            return memberResult.MapTo<RecurrencePreviewResponseDto>();

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var spec = new RecurrenceEvaluator.RecurrenceSpec(
            (RecurrenceMode)request.RecurrenceMode, request.Weekdays, request.DayOfMonth,
            request.Interval, anchorDate, endDate);

        var occurrences = RecurrenceEvaluator
            .GetOccurrences(spec, today, today.AddDays(90))
            .Take(3)
            .Select(d => d.ToString("yyyy-MM-dd"))
            .ToList();

        var response = new RecurrencePreviewResponseDto
        {
            NextOccurrences = occurrences,
            Summary = RecurrenceEvaluator.Describe(spec, describeLoc)
        };

        return Result<RecurrencePreviewResponseDto>.Success(response);
    }

    // --- Helpers ---

    private sealed record MemberContext(Group Group, User CurrentUser, GroupMember Membership);

    private async Task<Result<MemberContext>> GetMemberContextAsync(Guid groupGuid, Guid currentUserId)
    {
        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<MemberContext>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<MemberContext>.NotFound(loc["GroupNotFound"]);

        var membership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (membership == null)
            return Result<MemberContext>.Forbidden(loc["AccessNotAllowed"]);

        return Result<MemberContext>.Success(new MemberContext(group, currentUser, membership));
    }

    /// <summary>
    /// Resolves the payer alias for alias-mode groups. In alias-mode, the payer must have an
    /// assigned alias (PaidByAliasId would otherwise be null and the expense silently excluded
    /// from alias balances); an explicit PaidByAliasId on the request overrides the membership alias.
    /// Returns null alias id for non-alias groups.
    /// </summary>
    private async Task<Result<int?>> ResolvePayerAliasAsync(Group group, string? paidByAliasIdString, User paidByUser)
    {
        if (!group.UseAliases)
            return Result<int?>.Success(null);

        if (!string.IsNullOrWhiteSpace(paidByAliasIdString))
        {
            if (!Guid.TryParse(paidByAliasIdString, out var paidByAliasGuid))
                return Result<int?>.BadRequest(loc["InvalidAliasId"]);

            var alias = await unitOfWork.Aliases
                .FirstOrDefaultAsync(a => a.Guid == paidByAliasGuid && a.DeletedAt == null);

            if (alias == null || alias.GroupId != group.Id)
                return Result<int?>.BadRequest(loc["AliasNotInGroup"]);

            return Result<int?>.Success(alias.Id);
        }

        var payerMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

        if (payerMembership?.AliasId == null)
            return Result<int?>.BadRequest(loc["PayerMissingAlias"]);

        return Result<int?>.Success(payerMembership.AliasId);
    }

    /// <summary>
    /// Validates split payloads and resolves the referenced users/aliases (must be group members /
    /// group aliases). Mirrors the validation in ExpenseCreationService: duplicates rejected,
    /// amounts positive, splits sum to amount within 0.001. Returns dictionaries keyed by
    /// Guid-string (the request IDs) so callers can project request order onto validated entities.
    /// </summary>
    private async Task<Result<(Dictionary<string, User> Users, Dictionary<string, Alias> Aliases)>> ValidateAndBuildSplitsAsync(
        Group group, decimal amount,
        List<CreateRecurringExpenseSplitDto> splits, List<CreateRecurringExpenseAliasSplitDto>? aliasSplits)
    {
        if (group.UseAliases)
        {
            if (aliasSplits == null || aliasSplits.Count == 0)
                return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(loc["AliasSplitRequired"]);

            var aliasIds = aliasSplits.Select(s => s.AliasId).ToList();
            var duplicateAliases = aliasIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateAliases.Count > 0)
                return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                    string.Format(loc["DuplicateAliasesInSplits"], string.Join(", ", duplicateAliases)));

            var validatedAliases = new Dictionary<string, Alias>();
            var totalSplitAmount = 0m;

            foreach (var split in aliasSplits)
            {
                if (!Guid.TryParse(split.AliasId, out var aliasGuid))
                    return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                        string.Format(loc["InvalidAliasIdInSplit"], split.AliasId));

                var alias = await unitOfWork.Aliases
                    .FirstOrDefaultAsync(a => a.Guid == aliasGuid && a.DeletedAt == null);

                if (alias == null)
                    return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                        string.Format(loc["AliasNotFoundInSplit"], split.AliasId));

                if (alias.GroupId != group.Id)
                    return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                        string.Format(loc["AliasNotInGroup"], split.AliasId));

                if (split.SplitAmount <= 0)
                    return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(loc["SplitAmountMustBePositive"]);

                validatedAliases[alias.Guid.ToString()] = alias;
                totalSplitAmount += split.SplitAmount;
            }

            var diff = Math.Abs(totalSplitAmount - amount);
            if (diff > 0.001m)
                return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                    string.Format(loc["SplitAmountsDoNotSum"], totalSplitAmount, amount));

            return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.Success(([], validatedAliases));
        }
        else
        {
            if (splits == null || splits.Count == 0)
                return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(loc["ExpenseSplitRequired"]);

            var userIds = splits.Select(s => s.UserId).ToList();
            var duplicateUsers = userIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateUsers.Count > 0)
                return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                    string.Format(loc["DuplicateUsersInSplits"], string.Join(", ", duplicateUsers)));

            var validatedUsers = new Dictionary<string, User>();
            var totalSplitAmount = 0m;

            foreach (var split in splits)
            {
                if (!Guid.TryParse(split.UserId, out var splitUserGuid))
                    return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                        string.Format(loc["InvalidUserIdInSplit"], split.UserId));

                var splitUser = await unitOfWork.Users
                    .FirstOrDefaultAsync(u => u.Guid == splitUserGuid && u.DeletedAt == null);

                if (splitUser == null)
                    return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                        string.Format(loc["UserNotFoundInSplit"], split.UserId));

                var isSplitUserMember = await unitOfWork.GroupMembers
                    .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == splitUser.Id && gm.DeletedAt == null);

                if (!isSplitUserMember)
                    return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                        string.Format(loc["UserNotMemberInSplit"], splitUser.FirstName, splitUser.LastName));

                if (split.SplitAmount <= 0)
                    return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(loc["SplitAmountMustBePositive"]);

                validatedUsers[splitUser.Guid.ToString()] = splitUser;
                totalSplitAmount += split.SplitAmount;
            }

            var diff = Math.Abs(totalSplitAmount - amount);
            if (diff > 0.001m)
                return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.BadRequest(
                    string.Format(loc["SplitAmountsDoNotSum"], totalSplitAmount, amount));

            return Result<(Dictionary<string, User>, Dictionary<string, Alias>)>.Success((validatedUsers, []));
        }
    }

    /// <summary>
    /// Spec sanity checks mirroring RecurrenceEvaluator.Validate's ArgumentException guards,
    /// expressed as Result failures so the API never throws for bad recurrence input.
    /// </summary>
    private Result ValidateSpec(
        RecurrenceMode mode, int weekdays, int? dayOfMonth, int? interval)
    {
        switch (mode)
        {
            case RecurrenceMode.Weekly:
                if (weekdays == 0)
                    return Result.BadRequest(loc["WeeklyModeRequiresWeekday"]);
                if ((weekdays & ~0x7F) != 0)
                    return Result.BadRequest(loc["WeekdaysOutOfRange"]);
                break;
            case RecurrenceMode.Monthly:
                if (dayOfMonth is null || dayOfMonth < 1 || dayOfMonth > 31)
                    return Result.BadRequest(loc["InvalidDayOfMonth"]);
                break;
            case RecurrenceMode.EveryNWeeks:
            case RecurrenceMode.EveryNMonths:
                if (interval is null || interval < 1)
                    return Result.BadRequest(loc["InvalidInterval"]);
                break;
            default:
                return Result.BadRequest(loc["UnknownRecurrenceMode"]);
        }

        return Result.Success();
    }

    /// <summary>
    /// Maps a template entity to its DTO with computed values: NextOccurrence (first occurrence
    /// strictly after today; null when paused or schedule ended), Summary (via RecurrenceEvaluator),
    /// and the supplied pending-instance count.
    /// </summary>
    private async Task<RecurringExpenseTemplateDto> MapTemplate(RecurringExpenseTemplate template, int pendingCount)
    {
        var spec = new RecurrenceEvaluator.RecurrenceSpec(
            (RecurrenceMode)template.RecurrenceModeId, template.Weekdays,
            template.DayOfMonth, template.Interval, template.AnchorDate, template.EndDate);

        string? nextOccurrence = null;
        var summary = "";

        try
        {
            summary = RecurrenceEvaluator.Describe(spec, describeLoc);
        }
        catch (ArgumentException)
        {
            // Invalid spec persisted in data — degrade gracefully
            return new RecurringExpenseTemplateDto(template, null, "", pendingCount,
                template.Splits.ToList(), template.AliasSplits.ToList());
        }

        if (template.IsActive)
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            // First occurrence strictly after today, up to EndDate (or a bounded 10-year window)
            var windowEnd = template.EndDate.HasValue && template.EndDate.Value < today.AddYears(10)
                ? template.EndDate.Value
                : today.AddYears(10);

            var next = RecurrenceEvaluator.GetOccurrences(spec, today.AddDays(1), windowEnd)
                .Cast<DateOnly?>()
                .FirstOrDefault();

            nextOccurrence = next?.ToString("yyyy-MM-dd");
        }

        // Missed/skipped surfacing (computed, not persisted — spec D5). MissedCount is
        // only meaningful on a PAUSED template; active templates report 0 (Open Q4).
        // SkippedPeriodDates always reflects the recorded skip boundary if one exists.
        int missedCount = 0;
        List<string> skippedPeriodDates = [];

        var windowStart = template.ResumeFrom.HasValue && template.ResumeFrom.Value > template.AnchorDate
            ? template.ResumeFrom.Value
            : template.AnchorDate;

        try
        {
            if (template.ResumeFrom.HasValue && template.ResumeFrom.Value > template.AnchorDate)
            {
                // Skipped = occurrences in [AnchorDate, ResumeFrom) with NO instance
                var existingPeriods = await unitOfWork.RecurringExpenseInstances
                    .Where(i => i.TemplateId == template.Id && i.DeletedAt == null)
                    .Select(i => i.PeriodDate)
                    .ToListAsync();

                var skipped = RecurrenceEvaluator.GetOccurrences(
                        new RecurrenceEvaluator.RecurrenceSpec(
                            (RecurrenceMode)template.RecurrenceModeId, template.Weekdays,
                            template.DayOfMonth, template.Interval, template.AnchorDate, template.EndDate),
                        template.AnchorDate,
                        template.ResumeFrom.Value.AddDays(-1) > template.AnchorDate
                            ? template.ResumeFrom.Value.AddDays(-1)
                            : template.AnchorDate)
                    .Where(p => !existingPeriods.Contains(p))
                    .ToList();

                skippedPeriodDates = skipped.Select(d => d.ToString("yyyy-MM-dd")).ToList();
            }

            if (!template.IsActive)
            {
                var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
                var existingPeriods = await unitOfWork.RecurringExpenseInstances
                    .Where(i => i.TemplateId == template.Id && i.DeletedAt == null)
                    .Select(i => i.PeriodDate)
                    .ToListAsync();

                var windowEnd = template.EndDate.HasValue && template.EndDate.Value < today
                    ? template.EndDate.Value
                    : today;

                missedCount = RecurrenceEvaluator.GetOccurrences(
                        new RecurrenceEvaluator.RecurrenceSpec(
                            (RecurrenceMode)template.RecurrenceModeId, template.Weekdays,
                            template.DayOfMonth, template.Interval, template.AnchorDate, template.EndDate),
                        windowStart, windowEnd)
                    .Except(existingPeriods)
                    .Count();
            }
        }
        catch (ArgumentException)
        {
            // Invalid spec persisted in data — degrade gracefully (already handled for summary)
        }

        return new RecurringExpenseTemplateDto(
            template, nextOccurrence, summary, pendingCount,
            template.Splits.ToList(), template.AliasSplits.ToList(),
            missedCount, skippedPeriodDates);
    }

    private async Task SetHasAvatarAsync(ExpenseDto expenseDto, Expense expense, List<ExpenseSplit>? splits)
    {
        var userIds = splits?.Select(s => s.UserId).ToList() ?? [];
        userIds.Add(expense.PaidBy);

        var avatarUserIds = await unitOfWork.UserAvatars
            .Where(a => userIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToHashSetAsync();

        expenseDto.PaidByUser.HasAvatar = avatarUserIds.Contains(expense.PaidBy);

        if (splits != null)
        {
            for (var i = 0; i < splits.Count; i++)
            {
                expenseDto.Splits[i].User.HasAvatar = avatarUserIds.Contains(splits[i].UserId);
            }
        }
    }
}