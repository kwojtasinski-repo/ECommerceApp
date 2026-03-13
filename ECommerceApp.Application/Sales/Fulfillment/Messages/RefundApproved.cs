using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.Messages
{
    public record RefundApproved(
        int RefundId,
        int OrderId,
        IReadOnlyList<RefundApprovedItem> Items,
        DateTime OccurredAt) : IMessage;
}
