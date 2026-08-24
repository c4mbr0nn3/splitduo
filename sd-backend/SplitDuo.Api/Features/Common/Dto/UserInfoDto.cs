namespace SplitDuo.Api.Features.Common.Dto;

public class UserInfoDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public bool HasAvatar { get; set; }
}

public class UserBasicInfoDto
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public bool HasAvatar { get; set; }
}