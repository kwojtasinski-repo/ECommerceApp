namespace ECommerceApp.Application.Presale.Checkout.DTOs
{
    public sealed record AddToCartDto(string UserId, int ProductId, int Quantity);
}
