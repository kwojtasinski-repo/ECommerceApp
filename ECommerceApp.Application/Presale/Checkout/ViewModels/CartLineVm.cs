namespace ECommerceApp.Application.Presale.Checkout.ViewModels
{
    public sealed record CartLineVm(int ProductId, int Quantity, string? ProductName);
}
