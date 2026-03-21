using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record ProductNameChanged(
        int ProductId,
        string NewName,
        DateTime OccurredAt) : IMessage;
}
