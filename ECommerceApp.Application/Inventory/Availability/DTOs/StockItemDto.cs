namespace ECommerceApp.Application.Inventory.Availability.DTOs
{
    public sealed record StockItemDto(
        int Id,
        int ProductId,
        int Quantity,
        int ReservedQuantity,
        int AvailableQuantity);
}
