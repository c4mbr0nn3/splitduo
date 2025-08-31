using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Api.Features.Users.Dto;

public class UserDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public string FullName => $"{FirstName} {LastName ?? ""}";

    public UserDto()
    {
    }

    public UserDto(User user)
    {
        Id = user.Guid.ToString();
        Email = user.Email;
        FirstName = user.FirstName;
        LastName = user.LastName;
        CreatedAt = user.CreatedAt;
        UpdatedAt = user.UpdatedAt;
    }
}