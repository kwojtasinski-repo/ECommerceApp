using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class ProductDetailsVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public List<ProductImageVm> Images { get; set; } = new();
        public List<int> TagIds { get; set; } = new();
        public List<string> TagNames { get; set; } = new();

        public static ProductDetailsVm FromDomain(Domain.Catalog.Products.Product s) => new()
        {
            Id = s.Id.Value,
            Name = s.Name.Value,
            Cost = s.Cost.Amount,
            Description = s.Description.Value,
            CategoryId = s.CategoryId.Value,
            Status = s.Status.ToString(),
            TagIds = s.ProductTags.Select(t => t.TagId.Value).ToList()
        };
    }

    public class ProductImageVm
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
    }
}
