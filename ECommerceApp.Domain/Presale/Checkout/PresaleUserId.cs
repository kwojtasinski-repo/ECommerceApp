using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public sealed record PresaleUserId(string Value) : TypedId<string>(Value)
    {
        public static implicit operator PresaleUserId(string Value)
        {
            return new PresaleUserId(Value);
        }
    }
}
