using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Sales.Fulfillment.Messages
{
    public record RefundCustomerNotified(int RefundId) : IMessage;
}