using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Sales.Fulfillment.Messages
{
    public record RefundStockReturned(int RefundId) : IMessage;
}