using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.Rules
{
    public enum CouponRuleParameterType
    {
        Int,
        Decimal,
        String,
        Date
    }

    /// <summary>
    /// UI-facing description of one rule parameter.
    /// <para>
    /// <see cref="Min"/> and <see cref="Max"/> are <b>inclusive</b> bounds for the generated input
    /// control, not the authority on validity — the rule's <see cref="ICouponRuleParameterValidator"/>
    /// is. Where a rule requires a strictly positive value, an integer parameter states the smallest
    /// valid value (<c>productId</c> → 1) while a decimal one states 0, since no "next" decimal
    /// exists; the validator rejects the 0 the control would still allow.
    /// </para>
    /// <see cref="DefaultValue"/> must be formatted with <see cref="System.Globalization.CultureInfo.InvariantCulture"/>
    /// — it round-trips through the form into persisted RulesJson.
    /// </summary>
    public sealed record CouponRuleParameter(
        string Key,
        CouponRuleParameterType Type,
        bool Required,
        string DefaultValue,
        decimal? Min,
        decimal? Max,
        string Label);

    public interface ICouponRuleParameterSchema
    {
        string RuleName { get; }
        IReadOnlyList<CouponRuleParameter> GetParameters();
    }
}