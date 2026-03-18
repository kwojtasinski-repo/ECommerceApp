using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Sales.Coupons.Messages
{
    public record OrderPriceAdjusted(
        int OrderId,
        decimal NewPrice,
        decimal Delta,
        string AdjustmentType,
        int ReferenceId) : IMessage;
}
