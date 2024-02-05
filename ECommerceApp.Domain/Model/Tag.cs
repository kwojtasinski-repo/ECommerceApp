using System.Collections.Generic;

namespace ECommerceApp.Domain.Model
{
    public class Tag : BaseEntity
    {
        public string Name { get; set; }

        public ICollection<ItemTag> ItemTags { get; set; } = new List<ItemTag>();
    }
}
