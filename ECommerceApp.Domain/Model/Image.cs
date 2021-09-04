using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Image : BaseEntity
    {
        public string SourcePath { get; set; }
        public string Name { get; set; }
        public int? ItemId { get; set; }
        public Item Item { get; set; }
    }
}
