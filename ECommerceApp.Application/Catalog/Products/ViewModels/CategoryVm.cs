namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class CategoryVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }

        public static CategoryVm FromDomain(Domain.Catalog.Products.Category s) => new()
        {
            Id = s.Id.Value,
            Name = s.Name.Value,
            Slug = s.Slug.Value
        };
    }
}
