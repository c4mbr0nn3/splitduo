namespace SplitDuo.Core.Domain.Enums;

public enum PaymentMode
{
    Other = 1,
    Card = 2,
    Cash = 3,
    Transfer = 4,
    OnlineService = 5,
    TicketRestaurant = 6
}

public static class PaymentModeExtensions
{
    public static string ToDisplayName(this PaymentMode paymentMode) => paymentMode switch
    {
        PaymentMode.Other => "Other",
        PaymentMode.Card => "Card",
        PaymentMode.Cash => "Cash",
        PaymentMode.Transfer => "Transfer",
        PaymentMode.OnlineService => "Online Services",
        PaymentMode.TicketRestaurant => "Ticket Restaurant",
        _ => paymentMode.ToString()
    };
}