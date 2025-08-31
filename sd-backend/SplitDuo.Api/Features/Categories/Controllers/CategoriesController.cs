using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Categories.Dto;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Api.Features.Categories.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
public class CategoriesController : BaseApiController
{
    [HttpGet]
    public ActionResult<ApiResponseDto<List<CategoryDto>>> GetCategories()
    {
        var categories = Enum.GetValues<ExpenseCategory>()
            .Select(c => new CategoryDto(c))
            .ToList();

        var result = Result<List<CategoryDto>>.Success(categories);
        return HandleResult(result, "Categories retrieved successfully");
    }
}