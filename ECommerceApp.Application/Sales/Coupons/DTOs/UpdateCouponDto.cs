namespace ECommerceApp.Application.Sales.Coupons.DTOs
{
    public sealed class UpdateCouponDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
