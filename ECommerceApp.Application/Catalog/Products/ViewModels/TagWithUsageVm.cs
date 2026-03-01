using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class TagWithUsageVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public int TotalProductCount { get; set; }
        public List<string> TopProductNames { get; set; } = new();
        public bool HasMore => TotalProductCount > TopProductNames.Count;
    }
}
