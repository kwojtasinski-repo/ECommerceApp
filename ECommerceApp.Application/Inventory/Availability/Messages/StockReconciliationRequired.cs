using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Inventory.Availability.Messages
{
    public record StockReconciliationRequired(
        int OrderId,
        IReadOnlyList<StockOperationFailure> Failures,
        DateTime OccurredAt) : IMessage;
}
