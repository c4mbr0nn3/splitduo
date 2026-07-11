using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Api.Features.Invitations.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
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
    TimeProvider timeProvider) : IInvitationsService
{
    private const int TokenExpirationHours = 48;

    public async Task<Result<SendInvitationResponseDto>> SendInvitationAsync(string groupId, Guid currentUserId,
        SendInvitationRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<SendInvitationResponseDto>.BadRequest("Invalid group ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<SendInvitationResponseDto>.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<SendInvitationResponseDto>.NotFound("Group not found");

        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<SendInvitationResponseDto>.Forbidden("Access to this group is not allowed");

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<SendInvitationResponseDto>.Forbidden("Only group administrators can invite members");

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
                return Result<SendInvitationResponseDto>.Conflict("User is already a member of this group");

            // Add existing user directly
            var groupMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = existingUser.Id,
                Role = GroupRole.Member
            };

            unitOfWork.GroupMembers.Add(groupMember);

            // Send notification email
            await notificationService.EnqueueAsync(emailTemplateProvider.Render(new GroupMemberAddedModel
            {
                To = existingUser.Email, RecipientFirstName = existingUser.FirstName,
                AddedByFirstName = currentUser.FirstName, AddedByLastName = currentUser.LastName,
                GroupName = group.Name, GroupGuid = group.Guid
            }));

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
                        LastName = existingUser.LastName
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
            return Result<SendInvitationResponseDto>.Conflict("An invitation for this email is already pending");

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

        // Send invitation email
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new GroupInvitationModel
        {
            To = email, GroupName = group.Name,
            InviterFirstName = currentUser.FirstName, InviterLastName = currentUser.LastName,
            RawToken = rawToken
        }));

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
                    LastName = currentUser.LastName
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
            return Result<List<InvitationDto>>.BadRequest("Invalid group ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<List<InvitationDto>>.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<List<InvitationDto>>.NotFound("Group not found");

        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<List<InvitationDto>>.Forbidden("Access to this group is not allowed");

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<List<InvitationDto>>.Forbidden("Only group administrators can view invitations");

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

        var dtos = invitations.Select(it => new InvitationDto
        {
            Id = it.Guid.ToString(),
            Email = it.Email,
            InvitedBy = new UserInfoDto
            {
                Id = it.InvitedByUser.Guid.ToString(),
                Email = it.InvitedByUser.Email,
                FirstName = it.InvitedByUser.FirstName,
                LastName = it.InvitedByUser.LastName
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
            return Result<InvitationDto>.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(invitationId, out var invitationGuid))
            return Result<InvitationDto>.BadRequest("Invalid invitation ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<InvitationDto>.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<InvitationDto>.NotFound("Group not found");

        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<InvitationDto>.Forbidden("Access to this group is not allowed");

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<InvitationDto>.Forbidden("Only group administrators can resend invitations");

        var oldInvitation = await unitOfWork.InvitationTokens
            .FirstOrDefaultAsync(it =>
                it.Guid == invitationGuid &&
                it.GroupId == group.Id &&
                it.AcceptedAt == null &&
                it.RevokedAt == null);

        if (oldInvitation == null)
            return Result<InvitationDto>.NotFound("Invitation not found");

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

        // Send new email
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new GroupInvitationModel
        {
            To = oldInvitation.Email, GroupName = group.Name,
            InviterFirstName = currentUser.FirstName, InviterLastName = currentUser.LastName,
            RawToken = rawToken
        }));

        return Result<InvitationDto>.Success(new InvitationDto
        {
            Id = newInvitation.Guid.ToString(),
            Email = newInvitation.Email,
            InvitedBy = new UserInfoDto
            {
                Id = currentUser.Guid.ToString(),
                Email = currentUser.Email,
                FirstName = currentUser.FirstName,
                LastName = currentUser.LastName
            },
            GroupName = group.Name,
            InvitedAt = newInvitation.CreatedAt,
            ExpiresAt = newInvitation.ExpiresAt
        });
    }

    public async Task<Result> RevokeInvitationAsync(string groupId, string invitationId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(invitationId, out var invitationGuid))
            return Result.BadRequest("Invalid invitation ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound("Group not found");

        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result.Forbidden("Access to this group is not allowed");

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result.Forbidden("Only group administrators can revoke invitations");

        var invitation = await unitOfWork.InvitationTokens
            .FirstOrDefaultAsync(it =>
                it.Guid == invitationGuid &&
                it.GroupId == group.Id &&
                it.AcceptedAt == null &&
                it.RevokedAt == null);

        if (invitation == null)
            return Result.NotFound("Invitation not found");

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
                "This invitation link is invalid or has expired.");

        if (invitation.RevokedAt != null)
            return Result<ValidateInvitationResponseDto>.BadRequest(
                "This invitation link is no longer valid.");

        if (invitation.AcceptedAt != null)
            return Result<ValidateInvitationResponseDto>.BadRequest(
                "This invitation has already been accepted.");

        if (invitation.ExpiresAt < now)
            return Result<ValidateInvitationResponseDto>.BadRequest(
                "This invitation link has expired.");

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
            return Result.BadRequest("This invitation link is invalid or has expired.");

        if (invitation.RevokedAt != null)
            return Result.BadRequest("This invitation link is no longer valid.");

        if (invitation.AcceptedAt != null)
            return Result.BadRequest("This invitation has already been accepted.");

        if (invitation.ExpiresAt < now)
            return Result.BadRequest("This invitation link has expired.");

        // Check if email already has an account
        var existingUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == invitation.Email && u.DeletedAt == null);

        if (existingUser != null)
            return Result.BadRequest("An account with this email already exists. Please log in.");

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