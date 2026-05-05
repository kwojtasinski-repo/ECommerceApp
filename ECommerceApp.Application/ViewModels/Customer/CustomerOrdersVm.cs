using System;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerOrdersVm : BaseVm
    {
        public string Number { get; set; }
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public int CustomerId { get; set; }
        public string UserId { get; set; }
        public int? PaymentId { get; set; }
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; }
    }
}
