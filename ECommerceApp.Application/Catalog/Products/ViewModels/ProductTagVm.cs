namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class ProductTagVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }

        public static ProductTagVm FromDomain(Domain.Catalog.Products.Tag s) => new()
        {
            Id = s.Id.Value,
            Name = s.Name.Value,
            Slug = s.Slug.Value
        };
    }
}
