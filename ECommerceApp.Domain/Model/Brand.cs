using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Brand : BaseEntity
    {
        public string Name { get; set; }

        public ICollection<Item> Items { get; set; }
    }
}
