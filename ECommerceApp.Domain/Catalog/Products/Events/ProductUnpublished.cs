using System;

namespace ECommerceApp.Domain.Catalog.Products.Events
{
    public record ProductUnpublished(int ItemId, DateTime OccurredAt);
}
