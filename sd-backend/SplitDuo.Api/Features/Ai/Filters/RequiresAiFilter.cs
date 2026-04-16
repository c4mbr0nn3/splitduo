using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Options;

namespace SplitDuo.Api.Features.Ai.Filters;

public class RequiresAiFilter(IOptions<AiOptions> aiOptions) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (!aiOptions.Value.IsEnabled)
            ctx.Result = new ObjectResult(
                    ApiResponseDto<object>.ErrorResponse("SERVICE_UNAVAILABLE", "AI module is not configured."))
                { StatusCode = 503 };
    }

    public void OnActionExecuted(ActionExecutedContext ctx)
    {
    }
}