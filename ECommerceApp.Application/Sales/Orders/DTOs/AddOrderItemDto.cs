namespace ECommerceApp.Application.Sales.Orders.DTOs
{
    public sealed record AddOrderItemDto(
        int ItemId,
        int Quantity,
        decimal UnitCost,
        string UserId);
}
