using System.Collections.Generic;

namespace ECommerceApp.Domain.Model
{
    public class Type : BaseEntity
    {
        public string Name { get; set; }

        public ICollection<Item> Items { get; set; }
    }
}
