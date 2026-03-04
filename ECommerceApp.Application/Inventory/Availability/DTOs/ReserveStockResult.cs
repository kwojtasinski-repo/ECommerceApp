namespace ECommerceApp.Application.Inventory.Availability.DTOs
{
    public enum ReserveStockResult
    {
        Success,
        ProductSnapshotNotFound,
        ProductNotAvailable,
        StockNotFound,
        InsufficientStock
    }
}
