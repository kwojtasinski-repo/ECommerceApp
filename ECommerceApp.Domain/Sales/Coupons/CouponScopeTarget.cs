using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public sealed record CouponScopeTargetId(int Value) : TypedId<int>(Value);

    public class CouponScopeTarget
    {
        public CouponScopeTargetId Id { get; private set; }
        public CouponId CouponId { get; private set; }
        public string ScopeType { get; private set; }
        public int TargetId { get; private set; }           // plain int — no FK to Catalog
        public string TargetName { get; private set; }      // display-only snapshot

        private CouponScopeTarget() { }

        public static CouponScopeTarget Create(CouponId couponId, string scopeType, int targetId, string targetName)
        {
            if (couponId is null)
                throw new DomainException("CouponId is required.");
            if (string.IsNullOrWhiteSpace(scopeType))
                throw new DomainException("ScopeType is required.");
            if (targetId <= 0)
                throw new DomainException("TargetId must be positive.");

            return new CouponScopeTarget
            {
                CouponId = couponId,
                ScopeType = scopeType,
                TargetId = targetId,
                TargetName = targetName ?? string.Empty
            };
        }

        public void UpdateTargetName(string newName)
        {
            TargetName = newName ?? string.Empty;
        }
    }
}
