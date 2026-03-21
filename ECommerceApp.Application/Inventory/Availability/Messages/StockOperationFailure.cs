namespace ECommerceApp.Application.Inventory.Availability.Messages
{
    public record StockOperationFailure(
        int ProductId,
        int Quantity,
        StockOperationType OperationType);
}
