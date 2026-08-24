using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.PaymentModes.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Api.Features.PaymentModes.Controllers;

[ApiController]
[Route("api/v1/payment-modes")]
[Authorize]
public class PaymentModesController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<PaymentModeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponseDto<List<PaymentModeDto>>> GetPaymentModes()
    {
        var paymentModes = Enum.GetValues<PaymentMode>()
            .Select(pm => new PaymentModeDto(pm))
            .ToList();

        var result = Result<List<PaymentModeDto>>.Success(paymentModes);
        return HandleResult(result, "Payment modes retrieved successfully");
    }
}