using System;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Model
{
    public class Refund : BaseEntity
    {
        public string Reason { get; set; }
        public bool Accepted { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } // 1:Many one customer can refund many orders
        public int OrderId { get; set; } // 1:1 Only one Order can be refund
        public Order Order { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } // 1:Many with OrderItems
    }
}
