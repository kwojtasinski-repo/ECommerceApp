using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record TagNameChanged(
        int TagId,
        string NewName,
        DateTime OccurredAt) : IMessage;
}
