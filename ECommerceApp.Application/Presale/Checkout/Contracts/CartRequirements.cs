namespace ECommerceApp.Application.Presale.Checkout.Contracts
{
    public sealed class CartRequirements : ICartRequirements
    {
        public int MaxQuantityPerOrderLine { get; }

        public CartRequirements(int maxQuantityPerOrderLine)
        {
            MaxQuantityPerOrderLine = maxQuantityPerOrderLine;
        }
    }
}
