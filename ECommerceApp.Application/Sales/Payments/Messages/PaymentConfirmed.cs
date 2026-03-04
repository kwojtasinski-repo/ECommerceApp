using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Payments.Messages
{
    public record PaymentConfirmed(
        int OrderId,
        IReadOnlyList<PaymentConfirmedItem> Items,
        DateTime OccurredAt) : IMessage;

    public record PaymentConfirmedItem(int ProductId, int Quantity);
}
