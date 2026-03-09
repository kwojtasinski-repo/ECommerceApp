namespace ECommerceApp.Application.Sales.Orders.DTOs
{
    public sealed record UpdateOrderDto(
        int OrderId,
        int CustomerId,
        int CurrencyId);
}
