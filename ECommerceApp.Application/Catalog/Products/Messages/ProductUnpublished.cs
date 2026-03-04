using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record ProductUnpublished(int ProductId, DateTime OccurredAt) : IMessage;
}
