namespace ECommerceApp.Domain.Messaging
{
    public enum OutboxStatus : byte
    {
        Pending    = 0,
        Running    = 1,
        Dispatched = 2,
        Failed     = 3,
        DeadLetter = 4
    }
}
