using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public sealed record PresaleProductId(int Value) : TypedId<int>(Value)
    {
        public static implicit operator PresaleProductId(int Value)
        {
            return new PresaleProductId(Value);
        }
    }
}
