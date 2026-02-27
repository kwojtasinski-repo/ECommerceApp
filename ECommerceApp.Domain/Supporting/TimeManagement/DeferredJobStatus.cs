namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public enum DeferredJobStatus : byte
    {
        Pending    = 0,
        Running    = 1,
        Failed     = 2,
        DeadLetter = 3
    }
}
