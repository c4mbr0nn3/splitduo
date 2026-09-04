namespace SplitDuo.Api.Features.System.Dto;

/// <summary>An aggregated admin notification returned to the client.</summary>
public record AdminNotificationDto(string Type, string TargetKey, object? Payload);

/// <summary>Request body for dismissing an admin notification.</summary>
public record DismissNotificationRequestDto(string Type, string TargetKey);