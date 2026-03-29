using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.Rules.Evaluators;
using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Sales.Coupons.Rules
{
    internal static class Extensions
    {
        public static IServiceCollection AddCouponRuleEngine(this IServiceCollection services)
        {
            services.AddScoped<ICouponRuleRegistry>(sp =>
            {
                var couponUsed = sp.GetRequiredService<ICouponUsedRepository>();
                var specialEvent = sp.GetRequiredService<ISpecialEventCache>();
                var stockChecker = sp.GetRequiredService<IStockAvailabilityChecker>();
                var orderCounter = sp.GetRequiredService<ICompletedOrderCounter>();

                var orderTotalEval = new OrderTotalScopeEvaluator();
                var perProductEval = new PerProductScopeEvaluator();
                var perCategoryEval = new PerCategoryScopeEvaluator();
                var perTagEval = new PerTagScopeEvaluator();
                var percentOffEval = new PercentageOffEvaluator();
                var fixedAmountEval = new FixedAmountOffEvaluator();
                var freeItemEval = new FreeItemEvaluator();
                var giftProductEval = new GiftProductEvaluator();
                var freeCheapestEval = new FreeCheapestItemEvaluator();
                var maxUsesEval = new MaxUsesEvaluator(couponUsed);
                var maxUsesPerUserEval = new MaxUsesPerUserEvaluator(couponUsed);
                var validDateEval = new ValidDateRangeEvaluator();
                var minOrderEval = new MinOrderValueEvaluator();
                var specialEventEval = new SpecialEventEvaluator(specialEvent);
                var firstPurchaseEval = new FirstPurchaseOnlyEvaluator(orderCounter);
                var giftProductAsyncEval = new GiftProductAsyncEvaluator(stockChecker);

                return new CouponWorkflowBuilder()
                    .DefineRule(CouponRuleNames.OrderTotal, CouponRuleCategory.Scope,
                        orderTotalEval, null, orderTotalEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.PerProduct, CouponRuleCategory.Scope,
                        perProductEval, null, perProductEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.PerCategory, CouponRuleCategory.Scope,
                        perCategoryEval, null, perCategoryEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.PerTag, CouponRuleCategory.Scope,
                        perTagEval, null, perTagEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.PercentageOff, CouponRuleCategory.Discount,
                        percentOffEval, null, percentOffEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.FixedAmountOff, CouponRuleCategory.Discount,
                        fixedAmountEval, null, fixedAmountEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.FreeItem, CouponRuleCategory.Discount,
                        freeItemEval, null, freeItemEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.GiftProduct, CouponRuleCategory.Discount,
                        giftProductEval, giftProductAsyncEval, giftProductEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.FreeCheapestItem, CouponRuleCategory.Discount,
                        freeCheapestEval, null, freeCheapestEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.MaxUses, CouponRuleCategory.Constraint,
                        null, maxUsesEval, maxUsesEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.MaxUsesPerUser, CouponRuleCategory.Constraint,
                        null, maxUsesPerUserEval, maxUsesPerUserEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.ValidDateRange, CouponRuleCategory.Constraint,
                        validDateEval, null, validDateEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.MinOrderValue, CouponRuleCategory.Constraint,
                        minOrderEval, null, minOrderEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.SpecialEvent, CouponRuleCategory.Constraint,
                        null, specialEventEval, specialEventEval as ICouponRuleParameterValidator)
                    .DefineRule(CouponRuleNames.FirstPurchaseOnly, CouponRuleCategory.Constraint,
                        null, firstPurchaseEval, firstPurchaseEval as ICouponRuleParameterValidator)
                    .Build();
            });

            services.AddScoped<ICouponRulePipeline, CouponRulePipeline>();
            return services;
        }
    }
}
