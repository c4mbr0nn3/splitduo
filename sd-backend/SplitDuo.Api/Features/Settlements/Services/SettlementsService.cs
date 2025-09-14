using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Settlements.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Settlements.Services;

// Simple data structure for balance calculations
public class SettlementBalanceData
{
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public decimal Amount { get; set; }
}

public interface ISettlementsService
{
    Task<Result<PaginatedResponseDto<SettlementDto>>> GetGroupSettlementsAsync(
        string groupId, Guid currentUserId, int page, int limit, string? startDate, string? endDate);

    Task<Result<SettlementDto>> CreateSettlementAsync(string groupId, Guid currentUserId,
        CreateSettlementRequestDto request);

    Task<Result<SettlementDto>> UpdateSettlementAsync(string groupId, string settlementId, Guid currentUserId,
        UpdateSettlementRequestDto request);

    Task<Result> DeleteSettlementAsync(string groupId, string settlementId, Guid currentUserId);

    // Method for balance calculation - internal service communication
    Task<List<SettlementBalanceData>> GetSettlementsForBalanceCalculationAsync(int groupId);
}

public class SettlementsService(IUnitOfWork unitOfWork) : ISettlementsService
{
    public async Task<Result<PaginatedResponseDto<SettlementDto>>> GetGroupSettlementsAsync(
        string groupId, Guid currentUserId, int page, int limit, string? startDate, string? endDate)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<PaginatedResponseDto<SettlementDto>>.BadRequest("Invalid group ID format");

        // Validate page and limit
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<PaginatedResponseDto<SettlementDto>>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<PaginatedResponseDto<SettlementDto>>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<PaginatedResponseDto<SettlementDto>>.Forbidden("Access to this group is not allowed");

        // Build query
        var query = unitOfWork.Settlements
            .Where(s => s.GroupId == group.Id && s.DeletedAt == null)
            .Include(s => s.FromUser)
            .Include(s => s.ToUser)
            .Include(s => s.Group)
            .AsQueryable();

        // Apply date filters
        if (DateOnly.TryParse(startDate, out var startDateOnly))
            query = query.Where(s => s.SettlementDate >= startDateOnly);

        if (DateOnly.TryParse(endDate, out var endDateOnly))
            query = query.Where(s => s.SettlementDate <= endDateOnly);

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        // Apply pagination and ordering
        var settlements = await query
            .OrderByDescending(s => s.SettlementDate)
            .ThenByDescending(s => s.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        // Map to DTOs
        var settlementDtos = settlements.Select(settlement => new SettlementDto
        {
            Id = settlement.Guid.ToString(),
            GroupId = settlement.Group.Guid.ToString(),
            FromUserId = settlement.FromUser.Guid.ToString(),
            FromUser = new UserBasicInfoDto
            {
                Id = settlement.FromUser.Guid.ToString(),
                FirstName = settlement.FromUser.FirstName,
                LastName = settlement.FromUser.LastName
            },
            ToUserId = settlement.ToUser.Guid.ToString(),
            ToUser = new UserBasicInfoDto
            {
                Id = settlement.ToUser.Guid.ToString(),
                FirstName = settlement.ToUser.FirstName,
                LastName = settlement.ToUser.LastName
            },
            Amount = settlement.Amount,
            SettlementDate = settlement.SettlementDate.ToString("yyyy-MM-dd"),
            Description = settlement.Description,
            CreatedAt = settlement.CreatedAt,
            UpdatedAt = settlement.UpdatedAt
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

        var response = PaginatedResponseDto<SettlementDto>.SuccessResponse(settlementDtos, pagination);
        return Result<PaginatedResponseDto<SettlementDto>>.Success(response);
    }

    public async Task<Result<SettlementDto>> CreateSettlementAsync(string groupId, Guid currentUserId,
        CreateSettlementRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<SettlementDto>.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(request.FromUserId, out var fromUserGuid))
            return Result<SettlementDto>.BadRequest("Invalid from user ID format");

        if (!Guid.TryParse(request.ToUserId, out var toUserGuid))
            return Result<SettlementDto>.BadRequest("Invalid to user ID format");

        if (!DateOnly.TryParse(request.SettlementDate, out var settlementDate))
            return Result<SettlementDto>.BadRequest("Invalid settlement date format");

        if (request.FromUserId == request.ToUserId)
            return Result<SettlementDto>.BadRequest("From user and to user cannot be the same");

        if (request.Amount <= 0)
            return Result<SettlementDto>.BadRequest("Settlement amount must be greater than zero");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<SettlementDto>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<SettlementDto>.NotFound("Group not found");

        // Check if current user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<SettlementDto>.Forbidden("Access to this group is not allowed");

        // Find from and to users
        var fromUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == fromUserGuid && u.DeletedAt == null);

        if (fromUser == null)
            return Result<SettlementDto>.NotFound("From user not found");

        var toUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == toUserGuid && u.DeletedAt == null);

        if (toUser == null)
            return Result<SettlementDto>.NotFound("To user not found");

        // Check if both users are members of the group
        var areUsersMembers = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == group.Id && gm.DeletedAt == null)
            .Where(gm => gm.UserId == fromUser.Id || gm.UserId == toUser.Id)
            .CountAsync();

        if (areUsersMembers != 2)
            return Result<SettlementDto>.BadRequest("Both from user and to user must be members of this group");

        // Create settlement
        var settlement = new Settlement
        {
            GroupId = group.Id,
            FromUserId = fromUser.Id,
            ToUserId = toUser.Id,
            Amount = request.Amount,
            SettlementDate = settlementDate,
            Description = request.Description
        };

        unitOfWork.Settlements.Add(settlement);

        // Create response DTO
        var settlementDto = new SettlementDto
        {
            Id = settlement.Guid.ToString(),
            GroupId = group.Guid.ToString(),
            FromUserId = fromUser.Guid.ToString(),
            FromUser = new UserBasicInfoDto
            {
                Id = fromUser.Guid.ToString(),
                FirstName = fromUser.FirstName,
                LastName = fromUser.LastName
            },
            ToUserId = toUser.Guid.ToString(),
            ToUser = new UserBasicInfoDto
            {
                Id = toUser.Guid.ToString(),
                FirstName = toUser.FirstName,
                LastName = toUser.LastName
            },
            Amount = settlement.Amount,
            SettlementDate = settlement.SettlementDate.ToString("yyyy-MM-dd"),
            Description = settlement.Description,
            CreatedAt = settlement.CreatedAt,
            UpdatedAt = settlement.UpdatedAt
        };

        return Result<SettlementDto>.Success(settlementDto);
    }

    public async Task<Result<SettlementDto>> UpdateSettlementAsync(string groupId, string settlementId,
        Guid currentUserId, UpdateSettlementRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<SettlementDto>.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(settlementId, out var settlementGuid))
            return Result<SettlementDto>.BadRequest("Invalid settlement ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<SettlementDto>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<SettlementDto>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<SettlementDto>.Forbidden("Access to this group is not allowed");

        var settlement = await unitOfWork.Settlements
            .Include(s => s.FromUser)
            .Include(s => s.ToUser)
            .Include(s => s.Group)
            .FirstOrDefaultAsync(s => s.Guid == settlementGuid && s.GroupId == group.Id && s.DeletedAt == null);

        if (settlement == null)
            return Result<SettlementDto>.NotFound("Settlement not found");

        // Update properties if provided
        if (request.Amount.HasValue)
        {
            if (request.Amount.Value <= 0)
                return Result<SettlementDto>.BadRequest("Settlement amount must be greater than zero");

            settlement.Amount = request.Amount.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.SettlementDate))
        {
            if (!DateOnly.TryParse(request.SettlementDate, out var settlementDate))
                return Result<SettlementDto>.BadRequest("Invalid settlement date format");

            settlement.SettlementDate = settlementDate;
        }

        if (request.Description != null)
            settlement.Description = request.Description;

        // Create response DTO
        var settlementDto = new SettlementDto
        {
            Id = settlement.Guid.ToString(),
            GroupId = settlement.Group.Guid.ToString(),
            FromUserId = settlement.FromUser.Guid.ToString(),
            FromUser = new UserBasicInfoDto
            {
                Id = settlement.FromUser.Guid.ToString(),
                FirstName = settlement.FromUser.FirstName,
                LastName = settlement.FromUser.LastName
            },
            ToUserId = settlement.ToUser.Guid.ToString(),
            ToUser = new UserBasicInfoDto
            {
                Id = settlement.ToUser.Guid.ToString(),
                FirstName = settlement.ToUser.FirstName,
                LastName = settlement.ToUser.LastName
            },
            Amount = settlement.Amount,
            SettlementDate = settlement.SettlementDate.ToString("yyyy-MM-dd"),
            Description = settlement.Description,
            CreatedAt = settlement.CreatedAt,
            UpdatedAt = settlement.UpdatedAt
        };

        return Result<SettlementDto>.Success(settlementDto);
    }

    public async Task<Result> DeleteSettlementAsync(string groupId, string settlementId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(settlementId, out var settlementGuid))
            return Result.BadRequest("Invalid settlement ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result.Forbidden("Access to this group is not allowed");

        var settlement = await unitOfWork.Settlements
            .FirstOrDefaultAsync(s => s.Guid == settlementGuid && s.GroupId == group.Id && s.DeletedAt == null);

        if (settlement == null)
            return Result.NotFound("Settlement not found");

        // Soft delete the settlement
        settlement.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return Result.Success();
    }

    public async Task<List<SettlementBalanceData>> GetSettlementsForBalanceCalculationAsync(int groupId)
    {
        var settlements = await unitOfWork.Settlements
            .Where(s => s.GroupId == groupId && s.DeletedAt == null)
            .Select(s => new SettlementBalanceData
            {
                FromUserId = s.FromUserId,
                ToUserId = s.ToUserId,
                Amount = s.Amount
            })
            .ToListAsync();

        return settlements;
    }
}