using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Currency : BaseEntity
    {
        public string Code { get; set; }
        public string Description { get; set; }

        public ICollection<CurrencyRate> CurrencyRates { get; set; }
        public ICollection<Payment> Payments { get; set; }
        public ICollection<Order> Orders { get; set; }
        public ICollection<Item> Items { get; set; }
    }
}
