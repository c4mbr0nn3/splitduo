using System.Text.Json.Serialization;
using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Api.Features.Users.Dto;

public class UserDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public int GlobalRoleId { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public string FullName => $"{FirstName} {LastName ?? ""}";
    public bool TwoFactorEnabled { get; set; }

    [JsonIgnore] public int OriginalId { get; set; }

    public UserDto()
    {
    }

    public UserDto(User user)
    {
        Id = user.Guid.ToString();
        OriginalId = user.Id;
        Email = user.Email;
        FirstName = user.FirstName;
        LastName = user.LastName;
        GlobalRoleId = user.GlobalRoleId;
        CreatedAt = user.CreatedAt;
        UpdatedAt = user.UpdatedAt;
        TwoFactorEnabled = user.TwoFactorEnabled;
    }
}