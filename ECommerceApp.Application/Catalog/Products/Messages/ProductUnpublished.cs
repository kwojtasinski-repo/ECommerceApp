using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Catalog.Products;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record ProductUnpublished(int ProductId, UnpublishReason Reason, DateTime OccurredAt) : IMessage;
}
