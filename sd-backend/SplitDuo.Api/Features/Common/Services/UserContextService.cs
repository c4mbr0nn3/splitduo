using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Common.Services;

public interface IUserContextService
{
    Guid? GetCurrentUserId();
    Task<User?> GetCurrentUserAsync();
    bool IsAuthenticated();
    GlobalRole? GetCurrentUserGlobalRole();
    bool IsSystemAdmin();
}

public class UserContextService(
    IHttpContextAccessor httpContextAccessor,
    IUnitOfWork unitOfWork) : IUserContextService
{
    public Guid? GetCurrentUserId()
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User
            .FindFirst("userId")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return null;

        return await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userId);
    }

    public bool IsAuthenticated()
    {
        return httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
    }

    public GlobalRole? GetCurrentUserGlobalRole()
    {
        var roleClaim = httpContextAccessor.HttpContext?.User
            .FindFirst("role")?.Value;
        return Enum.TryParse<GlobalRole>(roleClaim, true, out var role) ? role : null;
    }

    public bool IsSystemAdmin()
    {
        return GetCurrentUserGlobalRole() == GlobalRole.SystemAdmin;
    }
}