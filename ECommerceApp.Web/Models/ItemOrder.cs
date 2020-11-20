using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Models
{
    public class ItemOrder
    {
        public ItemOrder()
        {
            Order = new HashSet<Order>();
        }

        [DisplayName("Ordered Quantity")]
        public int ItemOrderQuantity { get; set; }
        public Item Item { get; set; }
        public virtual ICollection<Order> Order { get; set; }
    }
}
