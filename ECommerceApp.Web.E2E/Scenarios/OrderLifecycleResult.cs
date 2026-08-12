namespace ECommerceApp.Web.E2E.Scenarios
{
    public sealed record OrderLifecycleResult(
        int OrderId,
        bool PaymentConfirmed,
        string FinalShipmentStatus);
}