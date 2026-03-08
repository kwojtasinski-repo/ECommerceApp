namespace ECommerceApp.Domain.Presale.Checkout
{
    public class CartLine
    {
        public PresaleUserId UserId { get; private set; } = default!;
        public PresaleProductId ProductId { get; private set; } = default!;
        public Shared.Quantity Quantity { get; private set; } = default!;

        private CartLine() { }

        public static CartLine Create(string userId, int productId, int quantity)
            => new CartLine
            {
                UserId = new PresaleUserId(userId),
                ProductId = new PresaleProductId(productId),
                Quantity = new Shared.Quantity(quantity)
            };

        public void UpdateQuantity(int quantity)
        {
            Quantity = new Shared.Quantity(quantity);
        }
    }
}
