namespace ECommerceApp.Application.Presale.Checkout.ViewModels
{
    public sealed class CartItemVm
    {
        public int CartItemId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
