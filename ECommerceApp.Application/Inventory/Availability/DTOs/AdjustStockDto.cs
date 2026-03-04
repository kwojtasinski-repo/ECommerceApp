namespace ECommerceApp.Application.Inventory.Availability.DTOs
{
    public sealed record AdjustStockDto(int ProductId, int NewQuantity);
}
