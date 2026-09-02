using ECommerceApp.Domain.Sales.Coupons.ValueObjects;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public sealed record CouponScopeTargetId(int Value) : TypedId<int>(Value);

    public class CouponScopeTarget
    {
        public CouponScopeTargetId Id { get; private set; }
        public CouponId CouponId { get; private set; }
        public CouponScopeType ScopeType { get; private set; }
        public int TargetId { get; private set; }           // plain int — no FK to Catalog
        public string TargetName { get; private set; }      // display-only snapshot

        private CouponScopeTarget() { }

        public static CouponScopeTarget Create(CouponId couponId, string scopeType, int targetId, string targetName)
        {
            if (couponId is null)
            {
                throw new DomainException("CouponId is required.");
            }

            if (targetId <= 0)
            {
                throw new DomainException("TargetId must be positive.");
            }

            // Mirrors the nvarchar(200) TargetName column. Without this the snapshot name is only
            // bounded by the database, so a hand-posted admin form surfaces as an unhandled
            // DbUpdateException instead of a validation error.
            if (targetName is not null && targetName.Length > 200)
            {
                throw new DomainException("TargetName must not exceed 200 characters.");
            }

            return new CouponScopeTarget
            {
                CouponId = couponId,
                ScopeType = new CouponScopeType(scopeType),
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
