using System;

namespace ECommerceApp.Domain.Model
{
    public class Payment : BaseEntity
    {
        public string Number { get; set; }
        public decimal Cost { get; set; }
        public PaymentState State { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public Customer Customer { get; set; }
        public int OrderId { get; set; } // 1:1 Payment Order
        public virtual Order Order { get; set; }
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; }
    }
}
