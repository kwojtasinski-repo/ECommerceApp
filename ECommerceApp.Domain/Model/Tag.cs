using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Tag : BaseEntity
    {
        public string Name { get; set; }

        public ICollection<ItemTag> ItemTags { get; set; }
    }
}
