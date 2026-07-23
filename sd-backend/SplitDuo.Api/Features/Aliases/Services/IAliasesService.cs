using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Core.Common;

namespace SplitDuo.Api.Features.Aliases.Services;

public interface IAliasesService
{
    Task<Result<List<AliasDto>>> ListAliasesAsync(string groupId, Guid currentUserId);
    Task<Result<AliasDto>> CreateAliasAsync(string groupId, Guid currentUserId, CreateAliasRequestDto request);
    Task<Result<AliasDto>> UpdateAliasAsync(string aliasId, Guid currentUserId, UpdateAliasRequestDto request);
    Task<Result> DeleteAliasAsync(string aliasId, Guid currentUserId);
    Task<Result<AliasDto>> AssignMemberAsync(string aliasId, Guid currentUserId, AssignAliasMemberRequestDto request);
    Task<Result> RemoveMemberAsync(string aliasId, string userId, Guid currentUserId);
    Task<Result> FinalizeAliasSetupAsync(string groupId, Guid currentUserId);
}
