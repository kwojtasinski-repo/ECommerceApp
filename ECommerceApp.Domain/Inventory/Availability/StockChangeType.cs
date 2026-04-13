namespace ECommerceApp.Domain.Inventory.Availability
{
    public enum StockChangeType : byte
    {
        Initialized = 0,
        Reserved    = 1,
        Released    = 2,
        Fulfilled   = 3,
        Returned    = 4,
        Adjusted    = 5,
        Withdrawn   = 6,
    }
}
