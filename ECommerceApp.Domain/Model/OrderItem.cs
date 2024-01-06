namespace ECommerceApp.Domain.Model
{
    public class OrderItem : BaseEntity
    {
        public int ItemId { get; set; }   // 1:Many Item OrderItem  
        public int ItemOrderQuantity { get; set; }
        public virtual Item Item { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public int? OrderId { get; set; }  // Many : 1 OrderItem Order
        public Order Order { get; set; }
        public int? CouponUsedId { get; set; }
        public CouponUsed CouponUsed { get; set; } // 1:Many OrderItem Coupon discount can be used for many Items
        public int? RefundId { get; set; } // 1:Many Refund OrderItem
        public Refund Refund { get; set; } 
    }
}
