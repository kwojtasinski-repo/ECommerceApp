using System;

namespace ECommerceApp.Application.Sales.Fulfillment.ViewModels
{
    public sealed record RefundVm(
        int Id,
        int OrderId,
        string Reason,
        bool OnWarranty,
        string Status,
        DateTime RequestedAt,
        DateTime? ProcessedAt,
        string UserId);
}
