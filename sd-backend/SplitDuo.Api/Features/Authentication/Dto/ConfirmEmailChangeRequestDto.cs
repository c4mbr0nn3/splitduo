using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class ConfirmEmailChangeRequestDto
{
    [Required]
    public string Token { get; set; } = "";
}
