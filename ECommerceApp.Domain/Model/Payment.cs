using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Payment : BaseEntity
    {
        public int Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public Customer Customer { get; set; }
        public int OrderId { get; set; } // 1:1 Payment Order
        public virtual Order Order { get; set; }
    }
}
