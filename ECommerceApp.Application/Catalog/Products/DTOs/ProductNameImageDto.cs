namespace ECommerceApp.Application.Catalog.Products.DTOs
{
    public sealed record ProductNameImageDto(int Id, string Name, string ImageFileName, string ImageUrl, int? MainImageId);
}
