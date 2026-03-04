using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record ProductPublished(
        int ProductId,
        string ProductName,
        bool IsDigital,
        DateTime OccurredAt) : IMessage;
}
