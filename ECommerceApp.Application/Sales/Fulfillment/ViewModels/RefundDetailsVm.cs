using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.ViewModels
{
    public sealed record RefundItemVm(int ProductId, int Quantity);

    public sealed record RefundDetailsVm(
        int Id,
        int OrderId,
        string Reason,
        bool OnWarranty,
        string Status,
        DateTime RequestedAt,
        DateTime? ProcessedAt,
        IReadOnlyList<RefundItemVm> Items);
}
