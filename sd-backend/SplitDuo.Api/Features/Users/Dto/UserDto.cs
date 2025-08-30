namespace SplitDuo.Api.Features.Users.Dto;

public class UserDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}