using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.DTOs
{
    public sealed class CouponRulesFormDto
    {
        public RuleSelectionDto Scope { get; set; }
        public RuleSelectionDto Discount { get; set; }
        public List<RuleSelectionDto> Constraints { get; set; } = new();
    }

    public sealed class RuleSelectionDto
    {
        public string Name { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}