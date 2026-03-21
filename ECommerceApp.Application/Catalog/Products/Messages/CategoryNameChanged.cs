using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record CategoryNameChanged(
        int CategoryId,
        string NewName,
        DateTime OccurredAt) : IMessage;
}
