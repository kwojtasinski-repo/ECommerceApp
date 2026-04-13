namespace ECommerceApp.Domain.Inventory.Availability
{
    public enum StockHoldStatus : byte
    {
        Guaranteed = 0,
        Confirmed  = 1,
        Released   = 2,
        Fulfilled  = 3,
        Withdrawn  = 4
    }
}
