using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Payments
{
    public sealed record PaymentOrderId : TypedId<int>
    {
        public PaymentOrderId(int value) : base(value)
        {
            if (value <= 0)
            {
                throw new DomainException("OrderId must be positive.");
            }
        }
    }
}
