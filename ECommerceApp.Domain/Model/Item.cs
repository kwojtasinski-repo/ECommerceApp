using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Item : BaseEntity
    {
        public string Name { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; }

        public virtual Brand Brand { get; set; }
        public virtual Type Type { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; }
        public ICollection<ItemTag> ItemTags { get; set; }
        public ICollection<Image> Images { get; set; }
    }
}
