namespace ECommerceApp.Domain.Sales.Coupons
{
    public static class CouponRuleNames
    {
        // Scope
        public const string OrderTotal = "order-total";
        public const string PerProduct = "per-product";
        public const string PerCategory = "per-category";
        public const string PerTag = "per-tag";

        // Discount
        public const string PercentageOff = "percentage-off";
        public const string FixedAmountOff = "fixed-amount-off";
        public const string FreeItem = "free-item";
        public const string GiftProduct = "gift-product";
        public const string FreeCheapestItem = "free-cheapest-item";

        // Constraint
        public const string MaxUses = "max-uses";
        public const string MaxUsesPerUser = "max-uses-per-user";
        public const string ValidDateRange = "valid-date-range";
        public const string MinOrderValue = "min-order-value";
        public const string SpecialEvent = "special-event";
        public const string FirstPurchaseOnly = "first-purchase-only";
    }
}
