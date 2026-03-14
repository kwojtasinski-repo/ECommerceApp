namespace ECommerceApp.Application.Sales.Orders.DTOs
{
    public sealed record PlaceOrderLineDto(int ProductId, int Quantity, decimal UnitPrice);
}
