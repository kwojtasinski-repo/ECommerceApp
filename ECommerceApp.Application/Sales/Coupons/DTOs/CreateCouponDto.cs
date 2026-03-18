using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.DTOs
{
    public sealed class CreateCouponDto
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public string RulesJson { get; set; }
        public List<ScopeTargetDto> ScopeTargets { get; set; } = new();
    }

    public sealed class ScopeTargetDto
    {
        public string ScopeType { get; set; }
        public int TargetId { get; set; }
        public string TargetName { get; set; }
    }
}
