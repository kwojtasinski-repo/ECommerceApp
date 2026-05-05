using ECommerceApp.Application.ViewModels.OrderItem;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderDetailsVm : BaseVm
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
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }
        public int Discount { get; set; }
        public string PaymentNumber { get; set; }
        public int PaymentCurrencyId { get; set; }
        public string PaymentCurrencyCode { get; set; }
        public bool AcceptedRefund { get; set; }
        public string ReasonRefund { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public string CustomerInformation { get; set; }

        public List<OrderItemDetailsVm> OrderItems { get; set; } = new List<OrderItemDetailsVm>();
    }
}
