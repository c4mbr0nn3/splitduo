using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.RecurringExpenses.Dto;

public class ToggleActiveRequestDto
{
    [Required] public bool IsActive { get; set; }
}