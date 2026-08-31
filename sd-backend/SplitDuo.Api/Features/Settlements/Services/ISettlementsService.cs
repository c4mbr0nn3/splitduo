using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Settlements.Dto;
using SplitDuo.Core.Common;

namespace SplitDuo.Api.Features.Settlements.Services;

public interface ISettlementsService
{
    Task<Result<PaginatedResponseDto<SettlementDto>>> GetGroupSettlementsAsync(
        string groupId, Guid currentUserId, int page, int limit);
    Task<Result<SettlementDto>> CreateSettlementAsync(
        string groupId, Guid currentUserId, CreateSettlementRequestDto request);
    Task<Result<SettlementDto>> GetSettlementAsync(
        string groupId, string settlementId, Guid currentUserId);
    Task<Result> DeleteSettlementAsync(
        string groupId, string settlementId, Guid currentUserId);
}