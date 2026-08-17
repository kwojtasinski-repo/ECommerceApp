namespace ECommerceApp.Domain.Sagas
{
    public enum SagaStepStatus : byte
    {
        Pending = 0,
        Completed = 1,
        Failed = 2,
        Compensated = 3
    }
}