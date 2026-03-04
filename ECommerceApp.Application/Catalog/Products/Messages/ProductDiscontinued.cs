using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record ProductDiscontinued(int ProductId, DateTime OccurredAt) : IMessage;
}
