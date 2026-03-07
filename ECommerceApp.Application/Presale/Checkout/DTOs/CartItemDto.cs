namespace ECommerceApp.Application.Presale.Checkout.DTOs
{
    public sealed record CartItemDto(
        int CartItemId,
        int ProductId,
        int Quantity,
        decimal UnitPrice);
}
