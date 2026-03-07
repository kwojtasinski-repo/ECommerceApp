namespace ECommerceApp.Domain.Presale.Checkout
{
    public class CartItem
    {
        public CartItemId Id { get; private set; } = new CartItemId(0);
        public CartId CartId { get; private set; } = default!;
        public int ProductId { get; private set; }
        public int Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }

        private CartItem() { }

        internal static CartItem Create(CartId cartId, int productId, int quantity, decimal unitPrice)
        {
            return new CartItem
            {
                CartId = cartId,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice
            };
        }

        internal void UpdateQuantity(int quantity)
        {
            Quantity = quantity;
        }
    }
}
