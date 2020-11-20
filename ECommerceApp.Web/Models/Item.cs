using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Models
{
    public class Item
    {
        [DisplayName("Item Id")]
        public int ItemId { get; set; }
        [DisplayName("Item Name")]
        public string ItemName { get; set; }
        [DisplayName("Item Type Name")]
        public string ItemTypeName { get; set; }
        [DisplayName("Item Cost")]
        public double ItemCost { get; set; }
        [DisplayName("Description")]
        public string ItemDescription { get; set; }
        [DisplayName("Quantity")]
        public int ItemQuantity { get; set; }
    }
}
