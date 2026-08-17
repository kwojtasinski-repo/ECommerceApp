namespace ECommerceApp.Domain.Sagas
{
    public enum SagaInstanceStatus : byte
    {
        Running = 0,
        Completed = 1,
        Compensating = 2,
        Failed = 3
    }
}