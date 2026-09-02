using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;

namespace ECommerceApp.Web.Areas.Sales
{
    /// <summary>
    /// Polish display text for the coupon rule vocabulary.
    /// <para>
    /// The schema's <c>CouponRuleParameter.Label</c> is deliberately left English and developer-facing;
    /// translation happens here, in the presentation layer, keyed by rule name and parameter key.
    /// Every rule in <see cref="CouponRuleNames"/> that the admin can select must have an entry —
    /// <c>CouponRuleDisplayNamesTests</c> fails the build if one is missing, so a newly added rule
    /// cannot silently reach the UI as raw kebab-case.
    /// </para>
    /// </summary>
    public static class CouponRuleDisplayNames
    {
        private static readonly IReadOnlyDictionary<string, string> Rules = new Dictionary<string, string>
        {
            [CouponRuleNames.OrderTotal] = "Całe zamówienie",
            [CouponRuleNames.PerProduct] = "Wybrany produkt",
            [CouponRuleNames.PerCategory] = "Wybrana kategoria",
            [CouponRuleNames.PerTag] = "Wybrany tag",
            [CouponRuleNames.PercentageOff] = "Rabat procentowy",
            [CouponRuleNames.FixedAmountOff] = "Rabat kwotowy",
            [CouponRuleNames.FreeItem] = "Darmowy produkt",
            [CouponRuleNames.GiftProduct] = "Produkt gratis",
            [CouponRuleNames.FreeCheapestItem] = "Najtańszy produkt gratis",
            [CouponRuleNames.MaxUses] = "Limit użyć",
            [CouponRuleNames.MaxUsesPerUser] = "Limit użyć na klienta",
            [CouponRuleNames.ValidDateRange] = "Zakres dat ważności",
            [CouponRuleNames.MinOrderValue] = "Minimalna wartość zamówienia",
            [CouponRuleNames.SpecialEvent] = "Wydarzenie specjalne",
            [CouponRuleNames.FirstPurchaseOnly] = "Tylko pierwszy zakup"
        };

        private static readonly IReadOnlyDictionary<string, string> Parameters = new Dictionary<string, string>
        {
            ["percent"] = "Procent rabatu",
            ["amount"] = "Kwota rabatu",
            ["productId"] = "Produkt",
            ["categoryId"] = "Kategoria",
            ["tagId"] = "Tag",
            ["quantity"] = "Ilość",
            ["maxFreeUnits"] = "Maksymalna liczba gratisów",
            ["maxUses"] = "Maksymalna liczba użyć",
            ["maxUsesPerUser"] = "Maksymalna liczba użyć na klienta",
            ["validFrom"] = "Ważny od",
            ["validTo"] = "Ważny do",
            ["minValue"] = "Minimalna wartość zamówienia",
            ["eventCode"] = "Kod wydarzenia"
        };

        private static readonly IReadOnlyDictionary<CouponRuleCategory, string> Categories =
            new Dictionary<CouponRuleCategory, string>
            {
                [CouponRuleCategory.Scope] = "Zakres",
                [CouponRuleCategory.Discount] = "Rabat",
                [CouponRuleCategory.Constraint] = "Ograniczenie"
            };

        public static string ForRule(string ruleName)
            => Rules.TryGetValue(ruleName, out var label) ? label : ruleName;

        public static string ForParameter(string parameterKey)
            => Parameters.TryGetValue(parameterKey, out var label) ? label : parameterKey;

        public static string ForCategory(CouponRuleCategory category)
            => Categories.TryGetValue(category, out var label) ? label : category.ToString();

        /// <summary>Parameter labels handed to the builder script, which renders inputs client-side.</summary>
        public static IReadOnlyDictionary<string, string> ParameterLabels => Parameters;
    }
}
