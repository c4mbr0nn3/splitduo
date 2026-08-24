using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Api.Features.Invitations.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Localization;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Invitations.Services;

public interface IInvitationsService
{
    Task<Result<SendInvitationResponseDto>> SendInvitationAsync(string groupId, Guid currentUserId,
        SendInvitationRequestDto request);

    Task<Result<List<InvitationDto>>> GetGroupInvitationsAsync(string groupId, Guid currentUserId);

    Task<Result<InvitationDto>> ResendInvitationAsync(string groupId, string invitationId, Guid currentUserId);

    Task<Result> RevokeInvitationAsync(string groupId, string invitationId, Guid currentUserId);

    Task<Result<ValidateInvitationResponseDto>> ValidateInvitationTokenAsync(string token);

    Task<Result> AcceptInvitationAsync(AcceptInvitationRequestDto request);

    Task<Result<List<PendingUserDto>>> GetPendingInvitationsAsync();
}

public class InvitationsService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    IEmailTemplateProvider emailTemplateProvider,
    IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider,
    IStringLocalizer<InvitationsService> loc) : IInvitationsService
{
    private const int TokenExpirationHours = 48;

    public async Task<Result<SendInvitationResponseDto>> SendInvitationAsync(string groupId, Guid currentUserId,
        SendInvitationRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<SendInvitationResponseDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<SendInvitationResponseDto>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<SendInvitationResponseDto>.NotFound(loc["GroupNotFound"]);

        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<SendInvitationResponseDto>.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<SendInvitationResponseDto>.Forbidden(loc["OnlyAdminsCanInvite"]);

        var email = request.Email.ToLowerInvariant();

        // Check if email belongs to an existing user
        var existingUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null);

        if (existingUser != null)
        {
            // Check if already a member
            var existingMembership = await unitOfWork.GroupMembers
                .FirstOrDefaultAsync(gm =>
                    gm.GroupId == group.Id && gm.UserId == existingUser.Id && gm.DeletedAt == null);

            if (existingMembership != null)
                return Result<SendInvitationResponseDto>.Conflict(loc["UserAlreadyMember"]);

            // Add existing user directly
            var groupMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = existingUser.Id,
                Role = GroupRole.Member
            };

            unitOfWork.GroupMembers.Add(groupMember);

            // Send notification email — use the existing user's UiLanguage (AC-005).
            var existingUserLanguage = SupportedLanguages.Normalize(existingUser.Settings.UiLanguage);
            await notificationService.EnqueueAsync(emailTemplateProvider.Render(new GroupMemberAddedModel
            {
                To = existingUser.Email, RecipientFirstName = existingUser.FirstName,
                AddedByFirstName = currentUser.FirstName, AddedByLastName = currentUser.LastName,
                GroupName = group.Name, GroupGuid = group.Guid
            }, existingUserLanguage));

            var hasAvatar = await unitOfWork.UserAvatars.AnyAsync(a => a.UserId == existingUser.Id);

            return Result<SendInvitationResponseDto>.Success(new SendInvitationResponseDto
            {
                Type = "member_added",
                Member = new GroupMemberDto
                {
                    GroupId = group.Guid.ToString(),
                    UserId = existingUser.Guid.ToString(),
                    User = new UserInfoDto
                    {
                        Id = existingUser.Guid.ToString(),
                        Email = existingUser.Email,
                        FirstName = existingUser.FirstName,
                        LastName = existingUser.LastName,
                        HasAvatar = hasAvatar
                    },
                    Role = GroupRole.Member.ToString().ToLowerInvariant(),
                    JoinedAt = groupMember.CreatedAt
                }
            });
        }

        // Check for existing pending invitation
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var existingInvitation = await unitOfWork.InvitationTokens
            .FirstOrDefaultAsync(it =>
                it.Email == email &&
                it.GroupId == group.Id &&
                it.AcceptedAt == null &&
                it.RevokedAt == null &&
                it.ExpiresAt > now);

        if (existingInvitation != null)
            return Result<SendInvitationResponseDto>.Conflict(loc["InvitationAlreadyPending"]);

        // Create invitation token
        var rawToken = GenerateToken();
        var invitationToken = new InvitationToken
        {
            Email = email,
            GroupId = group.Id,
            InvitedByUserId = currentUser.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = timeProvider.GetUtcNow().AddHours(TokenExpirationHours).ToUnixTimeSeconds()
        };

        unitOfWork.InvitationTokens.Add(invitationToken);

        // Send invitation email — use English as default for new users with no account (spec §9.4)
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new GroupInvitationModel
        {
            To = email, GroupName = group.Name,
            InviterFirstName = currentUser.FirstName, InviterLastName = currentUser.LastName,
            RawToken = rawToken
        }, "en"));

        var inviterHasAvatar = await unitOfWork.UserAvatars.AnyAsync(a => a.UserId == currentUser.Id);

        return Result<SendInvitationResponseDto>.Success(new SendInvitationResponseDto
        {
            Type = "invitation_sent",
            Invitation = new InvitationDto
            {
                Id = invitationToken.Guid.ToString(),
                Email = email,
                InvitedBy = new UserInfoDto
                {
                    Id = currentUser.Guid.ToString(),
                    Email = currentUser.Email,
                    FirstName = currentUser.FirstName,
                    LastName = currentUser.LastName,
                    HasAvatar = inviterHasAvatar
                },
                GroupName = group.Name,
                InvitedAt = invitationToken.CreatedAt,
                ExpiresAt = invitationToken.ExpiresAt
            }
        });
    }

    public async Task<Result<List<InvitationDto>>> GetGroupInvitationsAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<List<InvitationDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<List<InvitationDto>>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<List<InvitationDto>>.NotFound(loc["GroupNotFound"]);

        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<List<InvitationDto>>.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<List<InvitationDto>>.Forbidden(loc["OnlyAdminsCanInvite"]);

        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var invitations = await unitOfWork.InvitationTokens
            .Include(it => it.InvitedByUser)
            .Where(it =>
                it.GroupId == group.Id &&
                it.AcceptedAt == null &&
                it.RevokedAt == null &&
                it.ExpiresAt > now)
            .OrderByDescending(it => it.CreatedAt)
            .ToListAsync();

        var inviterIds = invitations.Select(it => it.InvitedByUserId).ToList();
        var avatarUserIds = await unitOfWork.UserAvatars
            .Where(a => inviterIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToHashSetAsync();

        var dtos = invitations.Select(it => new InvitationDto
        {
            Id = it.Guid.ToString(),
            Email = it.Email,
            InvitedBy = new UserInfoDto
            {
                Id = it.InvitedByUser.Guid.ToString(),
                Email = it.InvitedByUser.Email,
                FirstName = it.InvitedByUser.FirstName,
                LastName = it.InvitedByUser.LastName,
                HasAvatar = avatarUserIds.Contains(it.InvitedByUserId)
            },
            GroupName = group.Name,
            InvitedAt = it.CreatedAt,
            ExpiresAt = it.ExpiresAt
        }).ToList();

        return Result<List<InvitationDto>>.Success(dtos);
    }

    public async Task<Result<InvitationDto>> ResendInvitationAsync(string groupId, string invitationId,
        Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<InvitationDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(invitationId, out var invitationGuid))
            return Result<InvitationDto>.BadRequest(loc["InvalidInvitationIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<InvitationDto>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<InvitationDto>.NotFound(loc["GroupNotFound"]);

        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<InvitationDto>.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<InvitationDto>.Forbidden(loc["OnlyAdminsCanResend"]);

        var oldInvitation = await unitOfWork.InvitationTokens
            .FirstOrDefaultAsync(it =>
                it.Guid == invitationGuid &&
                it.GroupId == group.Id &&
                it.AcceptedAt == null &&
                it.RevokedAt == null);

        if (oldInvitation == null)
            return Result<InvitationDto>.NotFound(loc["InvitationNotFound"]);

        // Revoke old token
        oldInvitation.RevokedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        // Create new token
        var rawToken = GenerateToken();
        var newInvitation = new InvitationToken
        {
            Email = oldInvitation.Email,
            GroupId = group.Id,
            InvitedByUserId = currentUser.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = timeProvider.GetUtcNow().AddHours(TokenExpirationHours).ToUnixTimeSeconds()
        };

        unitOfWork.InvitationTokens.Add(newInvitation);

        // Send new email — use English as default for new users with no account (spec §9.4)
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new GroupInvitationModel
        {
            To = oldInvitation.Email, GroupName = group.Name,
            InviterFirstName = currentUser.FirstName, InviterLastName = currentUser.LastName,
            RawToken = rawToken
        }, "en"));

        var resenderHasAvatar = await unitOfWork.UserAvatars.AnyAsync(a => a.UserId == currentUser.Id);

        return Result<InvitationDto>.Success(new InvitationDto
        {
            Id = newInvitation.Guid.ToString(),
            Email = newInvitation.Email,
            InvitedBy = new UserInfoDto
            {
                Id = currentUser.Guid.ToString(),
                Email = currentUser.Email,
                FirstName = currentUser.FirstName,
                LastName = currentUser.LastName,
                HasAvatar = resenderHasAvatar
            },
            GroupName = group.Name,
            InvitedAt = newInvitation.CreatedAt,
            ExpiresAt = newInvitation.ExpiresAt
        });
    }

    public async Task<Result> RevokeInvitationAsync(string groupId, string invitationId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(invitationId, out var invitationGuid))
            return Result.BadRequest(loc["InvalidInvitationIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound(loc["GroupNotFound"]);

        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result.Forbidden(loc["OnlyAdminsCanRevoke"]);

        var invitation = await unitOfWork.InvitationTokens
            .FirstOrDefaultAsync(it =>
                it.Guid == invitationGuid &&
                it.GroupId == group.Id &&
                it.AcceptedAt == null &&
                it.RevokedAt == null);

        if (invitation == null)
            return Result.NotFound(loc["InvitationNotFound"]);

        invitation.RevokedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        return Result.Success();
    }

    public async Task<Result<ValidateInvitationResponseDto>> ValidateInvitationTokenAsync(string token)
    {
        var hashedToken = HashToken(token);
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var invitation = await unitOfWork.InvitationTokens
            .Include(it => it.Group)
            .FirstOrDefaultAsync(it => it.TokenHash == hashedToken);

        if (invitation == null)
            return Result<ValidateInvitationResponseDto>.BadRequest(
                loc["InvitationInvalidOrExpired"]);

        if (invitation.RevokedAt != null)
            return Result<ValidateInvitationResponseDto>.BadRequest(
                loc["InvitationNoLongerValid"]);

        if (invitation.AcceptedAt != null)
            return Result<ValidateInvitationResponseDto>.BadRequest(
                loc["InvitationAlreadyAccepted"]);

        if (invitation.ExpiresAt < now)
            return Result<ValidateInvitationResponseDto>.BadRequest(
                loc["InvitationExpired"]);

        return Result<ValidateInvitationResponseDto>.Success(new ValidateInvitationResponseDto
        {
            Email = invitation.Email,
            GroupName = invitation.Group.Name,
            ExpiresAt = invitation.ExpiresAt
        });
    }

    public async Task<Result> AcceptInvitationAsync(AcceptInvitationRequestDto request)
    {
        var hashedToken = HashToken(request.Token);
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var invitation = await unitOfWork.InvitationTokens
            .FirstOrDefaultAsync(it => it.TokenHash == hashedToken);

        if (invitation == null)
            return Result.BadRequest(loc["InvitationInvalidOrExpired"]);

        if (invitation.RevokedAt != null)
            return Result.BadRequest(loc["InvitationNoLongerValid"]);

        if (invitation.AcceptedAt != null)
            return Result.BadRequest(loc["InvitationAlreadyAccepted"]);

        if (invitation.ExpiresAt < now)
            return Result.BadRequest(loc["InvitationExpired"]);

        // Check if email already has an account
        var existingUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == invitation.Email && u.DeletedAt == null);

        if (existingUser != null)
            return Result.BadRequest(loc["AccountAlreadyExists"]);

        // Use transaction: need to save user first to get the generated Id,
        // then use that Id for GroupMember records
        await unitOfWork.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                Email = invitation.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PasswordHash = passwordHasher.HashPassword(null!, request.Password),
                GlobalRoleId = (int)GlobalRole.BaseUser
            };
            user.Settings.UiLanguage = SupportedLanguages.Normalize(request.UiLanguage);

            unitOfWork.Users.Add(user);
            await unitOfWork.SaveChangesAsync();

            // Find all pending invitations for this email and resolve them
            var pendingInvitations = await unitOfWork.InvitationTokens
                .Where(it =>
                    it.Email == invitation.Email &&
                    it.AcceptedAt == null &&
                    it.RevokedAt == null &&
                    it.ExpiresAt > now)
                .ToListAsync();

            foreach (var pending in pendingInvitations)
            {
                pending.AcceptedAt = now;

                var groupMember = new GroupMember
                {
                    GroupId = pending.GroupId,
                    UserId = user.Id,
                    Role = GroupRole.Member
                };

                unitOfWork.GroupMembers.Add(groupMember);
            }

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync();
            throw;
        }

        return Result.Success();
    }

    public async Task<Result<List<PendingUserDto>>> GetPendingInvitationsAsync()
    {
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var pendingInvitations = await unitOfWork.InvitationTokens
            .Include(it => it.Group)
            .Where(it =>
                it.AcceptedAt == null &&
                it.RevokedAt == null &&
                it.ExpiresAt > now)
            .ToListAsync();

        var grouped = pendingInvitations
            .GroupBy(it => it.Email)
            .Select(g => new PendingUserDto
            {
                Email = g.Key,
                Groups = g.Select(it => new PendingUserGroupDto
                {
                    Id = it.Group.Guid.ToString(),
                    Name = it.Group.Name,
                    InvitedAt = it.CreatedAt,
                    ExpiresAt = it.ExpiresAt
                }).ToList()
            })
            .ToList();

        return Result<List<PendingUserDto>>.Success(grouped);
    }

    private static string GenerateToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashedBytes);
    }
}