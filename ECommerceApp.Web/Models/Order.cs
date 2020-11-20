using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Models
{
    public class Order
    {
        public Order()
        {
            this.ItemOrder = new HashSet<ItemOrder>();
        }

        [DisplayName("Order Id")]
        public int OrderId { get; set; }
        [DisplayName("Order Number")]
        public int OrderNumber { get; set; }
        [DisplayName("Order Cost")]
        public double OrderCost { get; set; }
        public virtual ICollection<ItemOrder> ItemOrder { get; set; }
    }
}
