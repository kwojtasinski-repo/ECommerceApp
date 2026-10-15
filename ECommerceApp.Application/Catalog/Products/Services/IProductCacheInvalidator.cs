namespace ECommerceApp.Application.Catalog.Products.Services
{
    /// <summary>
    /// In-BC seam for evicting cached product data. Shared by everything in Catalog that changes what
    /// <see cref="IProductService.GetProductDetails"/> returns (product events, image add/delete).
    /// </summary>
    internal interface IProductCacheInvalidator
    {
        void InvalidateDetails(int productId);
    }
}
