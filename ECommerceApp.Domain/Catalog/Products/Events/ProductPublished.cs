using System;

namespace ECommerceApp.Domain.Catalog.Products.Events
{
    public record ProductPublished(int ItemId, DateTime OccurredAt);
}
