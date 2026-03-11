using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Payments
{
    public sealed record PaymentId(int Value) : TypedId<int>(Value);
}
