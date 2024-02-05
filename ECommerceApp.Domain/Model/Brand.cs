using System.Collections.Generic;

namespace ECommerceApp.Domain.Model
{
    public class Brand : BaseEntity
    {
        public string Name { get; set; }

        public ICollection<Item> Items { get; set; }
    }
}
