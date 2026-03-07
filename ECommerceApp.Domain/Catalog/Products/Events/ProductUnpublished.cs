using System;

namespace ECommerceApp.Domain.Catalog.Products.Events
{
    public record ProductUnpublished(int ItemId, UnpublishReason Reason, DateTime OccurredAt);
}
