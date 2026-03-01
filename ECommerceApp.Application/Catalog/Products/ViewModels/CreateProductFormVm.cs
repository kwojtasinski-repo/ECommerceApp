namespace ECommerceApp.Application.Catalog.Products.ViewModels
{
    public class CreateProductFormVm
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public string Tags { get; set; }
    }
}
