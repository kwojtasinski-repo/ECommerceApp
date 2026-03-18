using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.Rules
{
    public sealed class CouponEvaluationContext
    {
        public int OrderId { get; }
        public string UserId { get; }
        public decimal OriginalTotal { get; }
        public IReadOnlyList<CouponEvaluationItem> Items { get; }

        public CouponEvaluationContext(int orderId, string userId, decimal originalTotal, IReadOnlyList<CouponEvaluationItem> items)
        {
            OrderId = orderId;
            UserId = userId;
            OriginalTotal = originalTotal;
            Items = items;
        }
    }

    public sealed class CouponEvaluationItem
    {
        public int ProductId { get; }
        public int CategoryId { get; }
        public IReadOnlyList<int> TagIds { get; }
        public decimal UnitPrice { get; }
        public int Quantity { get; }

        public CouponEvaluationItem(int productId, int categoryId, IReadOnlyList<int> tagIds, decimal unitPrice, int quantity)
        {
            ProductId = productId;
            CategoryId = categoryId;
            TagIds = tagIds;
            UnitPrice = unitPrice;
            Quantity = quantity;
        }
    }
}
