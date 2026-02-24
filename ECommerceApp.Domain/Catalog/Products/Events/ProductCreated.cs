using System;

namespace ECommerceApp.Domain.Catalog.Products.Events
{
    public record ProductCreated(int ItemId, string Name, DateTime OccurredAt);
}
