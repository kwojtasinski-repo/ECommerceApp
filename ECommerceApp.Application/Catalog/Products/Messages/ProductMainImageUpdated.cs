using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record ProductMainImageUpdated(int ProductId, string FileName, DateTime OccurredAt) : IMessage;
}
