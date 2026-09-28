using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Ai.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Ai.Services;

public interface IAiUsageService
{
    Task<Result<PaginatedResponseDto<AiUsageEntryDto>>> GetUsageAsync(
        int page, int limit, long? from, long? to, Guid? userId, bool? success, CancellationToken ct = default);

    Task<Result<AiUsageSummaryDto>> GetSummaryAsync(
        long? from, long? to, Guid? userId, bool? success, CancellationToken ct = default);
}

public class AiUsageService(IUnitOfWork unitOfWork) : IAiUsageService
{
    public async Task<Result<PaginatedResponseDto<AiUsageEntryDto>>> GetUsageAsync(
        int page, int limit, long? from, long? to, Guid? userId, bool? success, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var query = ApplyFilters(unitOfWork.AiCallLogs.AsNoTracking().AsQueryable(), from, to, userId, success);

        var total = await query.CountAsync(ct);

        var projected = await query
            .OrderByDescending(l => l.RequestedAt)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(l => new
            {
                l.User.Guid,
                l.User.FirstName,
                l.User.LastName,
                l.User.Email,
                l.Feature,
                l.Model,
                l.Success,
                l.ErrorMessage,
                l.RequestedAt,
                l.RespondedAt,
                l.LatencyMs,
                l.InputTokens,
                l.OutputTokens,
                l.TotalTokens,
            })
            .ToListAsync(ct);

        var rows = projected.Select(l => new AiUsageEntryDto
        {
            UserId = l.Guid.ToString(),
            UserDisplayName = (l.FirstName + " " + (l.LastName ?? "")).Trim(),
            UserEmail = l.Email,
            Feature = l.Feature,
            Model = l.Model,
            Success = l.Success,
            ErrorMessage = l.ErrorMessage,
            RequestedAt = l.RequestedAt,
            RespondedAt = l.RespondedAt,
            LatencyMs = l.LatencyMs,
            InputTokens = l.InputTokens,
            OutputTokens = l.OutputTokens,
            TotalTokens = l.TotalTokens,
        }).ToList();

        var pagination = new PaginationDto
        {
            Page = page,
            Limit = limit,
            Total = total,
            TotalPages = (int)Math.Ceiling(total / (double)limit),
        };
        pagination.HasNext = page < pagination.TotalPages;
        pagination.HasPrev = page > 1;

        return Result<PaginatedResponseDto<AiUsageEntryDto>>.Success(
            PaginatedResponseDto<AiUsageEntryDto>.SuccessResponse(rows, pagination));
    }

    public async Task<Result<AiUsageSummaryDto>> GetSummaryAsync(
        long? from, long? to, Guid? userId, bool? success, CancellationToken ct = default)
    {
        var query = ApplyFilters(unitOfWork.AiCallLogs.AsNoTracking().AsQueryable(), from, to, userId, success);

        // Aggregation is in-memory over a flat projection so it is unit-testable without EF.
        // Memory is O(rows in range) — acceptable for an admin dashboard.
        var rows = await query
            .Select(l => new AiUsageRowProjection(
                l.RequestedAt, l.InputTokens, l.OutputTokens, l.TotalTokens,
                l.LatencyMs, l.Success, l.Model, l.User.Guid, l.User.Email))
            .ToListAsync(ct);

        return Result<AiUsageSummaryDto>.Success(AiUsageAggregator.Aggregate(rows));
    }

    private static IQueryable<AiCallLog> ApplyFilters(
        IQueryable<AiCallLog> query, long? from, long? to, Guid? userId, bool? success)
    {
        if (from.HasValue) query = query.Where(l => l.RequestedAt >= from.Value);
        if (to.HasValue) query = query.Where(l => l.RequestedAt <= to.Value);
        if (success.HasValue) query = query.Where(l => l.Success == success.Value);
        if (userId.HasValue) query = query.Where(l => l.User.Guid == userId.Value);
        return query;
    }
}