using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record ProductUpdated(int ProductId, DateTime OccurredAt) : IMessage;
}
