using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.DTOs
{
    public sealed class UpdateCouponDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CouponRulesFormDto Rules { get; set; } = new();
        public List<ScopeTargetDto> ScopeTargets { get; set; } = new();
        public byte[] Version { get; set; }
    }
}
