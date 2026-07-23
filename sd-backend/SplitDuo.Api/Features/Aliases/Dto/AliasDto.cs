using System.Text.Json.Serialization;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Api.Features.Aliases.Dto;

public class AliasDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string GroupId { get; set; } = "";
    public List<UserBasicInfoDto> Members { get; set; } = new();
    public bool IsSingleton { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }

    [JsonIgnore] public int OriginalId { get; set; }

    public AliasDto()
    {
    }

    public AliasDto(Alias alias, List<GroupMember>? members = null)
    {
        Id = alias.Guid.ToString();
        Name = alias.Name;
        GroupId = alias.Group?.Guid.ToString() ?? "";
        IsSingleton = alias.IsSingleton ?? false;
        CreatedAt = alias.CreatedAt;
        UpdatedAt = alias.UpdatedAt;
        OriginalId = alias.Id;
        Members = members?.Select(m => new UserBasicInfoDto
        {
            Id = m.User.Guid.ToString(),
            FirstName = m.User.FirstName,
            LastName = m.User.LastName
        }).ToList() ?? new List<UserBasicInfoDto>();
    }
}
