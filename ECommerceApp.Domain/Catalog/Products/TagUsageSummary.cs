using System.Collections.Generic;

namespace ECommerceApp.Domain.Catalog.Products
{
    public record TagUsageSummary(
        TagId Id,
        string Name,
        string Slug,
        int TotalProductCount,
        IReadOnlyList<string> TopProductNames);
}
