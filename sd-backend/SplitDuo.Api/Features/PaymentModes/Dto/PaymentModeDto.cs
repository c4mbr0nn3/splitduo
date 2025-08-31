using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Api.Features.PaymentModes.Dto;

public class PaymentModeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public PaymentModeDto()
    {
    }

    public PaymentModeDto(PaymentMode paymentMode)
    {
        Id = (int)paymentMode;
        Name = paymentMode.ToString();
    }
}