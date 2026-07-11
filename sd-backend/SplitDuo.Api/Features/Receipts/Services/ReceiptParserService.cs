using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Receipts.Services;

public interface IReceiptParserService
{
    Task<Result<ParsedReceiptDto>> ParseReceiptAsync(Stream imageStream);
}

public class ParsedReceiptDto
{
    public string Title { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string ExpenseDate { get; set; } = "";
    public int? CategoryId { get; set; }
    public int? PaymentModeId { get; set; }
}

public class ReceiptParserService(
    ChatClient chatClient,
    IOptions<AiOptions> aiOptions,
    IUnitOfWork unitOfWork,
    IUserContextService userContextService,
    TimeProvider timeProvider) : IReceiptParserService
{
    private const string PromptText = """
                                      You are a receipt parser. Extract the following fields from the receipt image and return a JSON object. Return ONLY the JSON object, no explanation, no markdown.

                                      {
                                        "title": "merchant name or store name",
                                        "amount": 0.00,
                                        "description": "brief summary of items or purchase type, max 255 characters, or null",
                                        "expenseDate": "YYYY-MM-DD",
                                        "categoryId": null,
                                        "paymentModeId": null
                                      }

                                      Category values (use the integer):
                                      1 = Other, 2 = Groceries, 3 = Transportation, 4 = Utilities, 5 = Entertainment,
                                      6 = Health, 7 = Education, 8 = Travel, 9 = Shopping, 10 = Housing, 11 = Dining

                                      Payment mode values (use the integer):
                                      1 = Other, 2 = Card, 3 = Cash, 4 = Transfer, 5 = Online Service, 6 = Ticket Restaurant

                                      Rules:
                                      - amount is the total amount paid, as a number (no currency symbol)
                                      - expenseDate must be in YYYY-MM-DD format; use today if not visible
                                      - categoryId must be one of the listed integers; pick the closest match
                                      - paymentModeId must be one of the listed integers, or null if not visible on the receipt
                                      - description must not exceed 255 characters
                                      - Return null for fields you cannot determine
                                      """;

    private readonly string _model = aiOptions.Value.Model!;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<ParsedReceiptDto>> ParseReceiptAsync(Stream imageStream)
    {
        var requestedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var user = await userContextService.GetCurrentUserAsync();
        if (user is null)
            return Result<ParsedReceiptDto>.Unauthorized("Unauthorized.");

        try
        {
            var imageBytes = await BinaryData.FromStreamAsync(imageStream);

            List<ChatMessage> messages =
            [
                new UserChatMessage(
                    ChatMessageContentPart.CreateImagePart(imageBytes, "image/jpeg"),
                    ChatMessageContentPart.CreateTextPart(PromptText)),
            ];

            var completionOptions = new ChatCompletionOptions { MaxOutputTokenCount = 500 };
            var response = await chatClient.CompleteChatAsync(messages, completionOptions);
            var respondedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();
            var usage = response.Value.Usage;
            var modelOutput = response.Value.Content[0].Text;
            var parsed = ExtractAndParseDto(modelOutput);

            unitOfWork.AiCallLogs.Add(new AiCallLog
            {
                UserId = user.Id,
                RequestedAt = requestedAt,
                RespondedAt = respondedAt,
                InputTokens = usage.InputTokenCount,
                OutputTokens = usage.OutputTokenCount,
                TotalTokens = usage.TotalTokenCount,
                Model = _model,
                Success = parsed is not null,
                ErrorMessage = parsed is null ? "Failed to parse receipt JSON" : null
            });
            await unitOfWork.SaveChangesAsync();

            return parsed is null
                ? Result<ParsedReceiptDto>.BadRequest("Failed to parse receipt. Check AI configuration.")
                : Result<ParsedReceiptDto>.Success(parsed);
        }
        catch (Exception ex)
        {
            unitOfWork.AiCallLogs.Add(new AiCallLog
            {
                UserId = user.Id,
                RequestedAt = requestedAt,
                RespondedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds(),
                Model = _model,
                Success = false,
                ErrorMessage = ex.Message
            });
            await unitOfWork.SaveChangesAsync();

            return Result<ParsedReceiptDto>.BadRequest("Failed to parse receipt. Check AI configuration.");
        }
    }

    private static ParsedReceiptDto? ExtractAndParseDto(string modelOutput)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelOutput)) return null;

            var match = Regex.Match(modelOutput, @"\{[\s\S]*\}");
            if (!match.Success) return null;

            var dto = JsonSerializer.Deserialize<ParsedReceiptDto>(match.Value, JsonOptions);

            if (dto is null) return null;

            if (dto.CategoryId.HasValue && !Enum.IsDefined(typeof(ExpenseCategory), dto.CategoryId.Value))
                dto.CategoryId = null;

            if (dto.PaymentModeId.HasValue && !Enum.IsDefined(typeof(PaymentMode), dto.PaymentModeId.Value))
                dto.PaymentModeId = null;

            if (dto.Amount is <= 0 or > 1_000_000)
                dto.Amount = 0;

            if (dto.Description?.Length > 255)
                dto.Description = dto.Description[..255];

            return dto;
        }
        catch
        {
            return null;
        }
    }
}