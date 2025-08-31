using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Api.Features.Categories.Dto;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public CategoryDto()
    {
    }

    public CategoryDto(ExpenseCategory category)
    {
        Id = (int)category;
        Name = category.ToString();
    }
}