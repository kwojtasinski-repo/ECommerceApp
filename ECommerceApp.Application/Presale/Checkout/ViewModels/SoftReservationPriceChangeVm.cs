namespace ECommerceApp.Application.Presale.Checkout.ViewModels
{
    public sealed record SoftReservationPriceChangeVm(int ProductId, decimal LockedPrice, decimal CurrentPrice);
}
