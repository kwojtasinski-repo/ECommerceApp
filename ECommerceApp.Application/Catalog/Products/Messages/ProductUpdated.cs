using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Products.Messages
{
    public record ProductUpdated(
        int ProductId,
        string Name,
        decimal Cost,
        string Description,
        int CategoryId,
        IReadOnlyList<int> TagIds,
        DateTime OccurredAt) : IMessage;
}
