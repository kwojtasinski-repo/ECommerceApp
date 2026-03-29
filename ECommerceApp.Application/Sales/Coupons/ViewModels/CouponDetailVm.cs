namespace ECommerceApp.Application.Sales.Coupons.ViewModels
{
    public sealed class CouponDetailVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string? RulesJson { get; init; }
    }
}
