using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Payments.ViewModels
{
    public sealed record PaymentVm(
        int Id,
        int OrderId,
        decimal TotalAmount,
        int CurrencyId,
        string Status,
        DateTime ExpiresAt,
        DateTime? ConfirmedAt);

    public sealed record PaymentDetailsVm(
        int Id,
        Guid PaymentId,
        int OrderId,
        decimal TotalAmount,
        int CurrencyId,
        string Status,
        DateTime ExpiresAt,
        DateTime? ConfirmedAt,
        string TransactionRef,
        string UserId);

    public sealed record PaymentListVm(
        IReadOnlyList<PaymentVm> Payments,
        int CurrentPage,
        int PageSize,
        int TotalCount);
}
