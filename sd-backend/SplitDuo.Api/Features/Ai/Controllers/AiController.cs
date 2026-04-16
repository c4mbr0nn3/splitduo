using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Options;

namespace SplitDuo.Api.Features.Ai.Controllers;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public class AiController(IOptions<AiOptions> aiOptions) : BaseApiController
{
    [HttpGet("status")]
    public ActionResult<ApiResponseDto<AiStatusDto>> GetStatus()
    {
        var dto = new AiStatusDto { Enabled = aiOptions.Value.IsEnabled };
        return Ok(ApiResponseDto<AiStatusDto>.SuccessResponse(dto));
    }
}

public class AiStatusDto
{
    public bool Enabled { get; set; }
}