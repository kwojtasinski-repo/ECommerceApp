using System;

namespace ECommerceApp.Domain.Catalog.Products.Events
{
    public record ProductDiscontinued(int ItemId, DateTime OccurredAt);
}
