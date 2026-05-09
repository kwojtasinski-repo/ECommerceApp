using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class ProductForListVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Status { get; set; }
        public int CategoryId { get; set; }
        public string MainImageUrl { get; set; }

        public static ProductForListVm FromDomain(Domain.Catalog.Products.Product s) => new()
        {
            Id = s.Id.Value,
            Name = s.Name.Value,
            Cost = s.Cost.Amount,
            CategoryId = s.CategoryId.Value,
            Status = s.Status.ToString()
        };
    }

    public class ProductListVm
    {
        public List<ProductForListVm> Products { get; set; } = new();
        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
