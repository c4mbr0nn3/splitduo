using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SplitDuo.Api.Features.Ai.Filters;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Receipts.Services;

namespace SplitDuo.Api.Features.Receipts.Controllers;

[ApiController]
[Route("api/v1/receipts")]
[Authorize]
public class ReceiptsController(IReceiptParserService receiptParserService) : BaseApiController
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private const string AcceptedImageMimeType = "image/jpeg";

    [HttpPost("parse")]
    [Consumes("multipart/form-data")]
    [ServiceFilter<RequiresAiFilter>]
    [EnableRateLimiting("receipt-scan")]
    public async Task<ActionResult<ApiResponseDto<ParsedReceiptDto>>> ParseReceipt(IFormFile image)
    {
        if (image is null)
            return BadRequest(ApiResponseDto<ParsedReceiptDto>.ErrorResponse(
                "INVALID_REQUEST", "No file uploaded."));
        if (image.Length > MaxImageSizeBytes)
            return BadRequest(ApiResponseDto<ParsedReceiptDto>.ErrorResponse(
                "FILE_TOO_LARGE", "Image must be under 5 MB."));
        if (image.ContentType != AcceptedImageMimeType)
            return BadRequest(ApiResponseDto<ParsedReceiptDto>.ErrorResponse(
                "INVALID_FILE_TYPE", "Only JPEG images are accepted."));

        await using var stream = image.OpenReadStream();
        var header = new byte[3];
        if (await stream.ReadAsync(header) != 3 || header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF)
            return BadRequest(ApiResponseDto<ParsedReceiptDto>.ErrorResponse(
                "INVALID_FILE_TYPE", "Only JPEG images are accepted."));
        stream.Position = 0;

        var result = await receiptParserService.ParseReceiptAsync(stream);

        if (result.IsFailure)
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponseDto<ParsedReceiptDto>.ErrorResponse(
                "BAD_GATEWAY", result.Error));

        return HandleResult(result, "Receipt parsed successfully");
    }
}