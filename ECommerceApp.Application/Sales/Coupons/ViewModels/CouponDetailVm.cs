using System.Collections.Generic;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Domain.Sales.Coupons;

namespace ECommerceApp.Application.Sales.Coupons.ViewModels
{
    public sealed class CouponDetailVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string RulesJson { get; init; }
        public IReadOnlyList<CouponRuleDefinition> Rules { get; init; } = new List<CouponRuleDefinition>();
        public IReadOnlyList<ScopeTargetDto> ScopeTargets { get; init; } = new List<ScopeTargetDto>();
        public byte[] Version { get; init; }
    }
}
