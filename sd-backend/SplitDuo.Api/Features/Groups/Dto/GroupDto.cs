using System.Text.Json.Serialization;

namespace SplitDuo.Api.Features.Groups.Dto;

public class GroupDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public int MemberCount { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    
    [JsonIgnore]
    public int OriginalId { get; set; }
}