using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Sales.Payments.ViewModels;
using ECommerceApp.Domain.Sales.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Services
{
    internal sealed class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IMessageBroker _broker;

        public PaymentService(IPaymentRepository paymentRepo, IMessageBroker broker)
        {
            _paymentRepo = paymentRepo;
            _broker = broker;
        }

        public async Task<PaymentDetailsVm> GetByIdAsync(int paymentId, CancellationToken ct = default)
        {
            var payment = await _paymentRepo.GetByIdAsync(paymentId, ct);
            return payment is null ? null : MapToDetailsVm(payment);
        }

        public async Task<PaymentDetailsVm> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
        {
            var payment = await _paymentRepo.GetByOrderIdAsync(orderId, ct);
            return payment is null ? null : MapToDetailsVm(payment);
        }

        public async Task<PaymentDetailsVm> GetByTokenAsync(Guid paymentId, string userId, CancellationToken ct = default)
        {
            var payment = await _paymentRepo.GetByPaymentIdAsync(paymentId, userId, ct);
            return payment is null ? null : MapToDetailsVm(payment);
        }

        public async Task<PaymentDetailsVm> GetPendingByOrderIdAsync(int orderId, string userId, CancellationToken ct = default)
        {
            var payment = await _paymentRepo.GetPendingByOrderIdAsync(orderId, userId, ct);
            return payment is null ? null : MapToDetailsVm(payment);
        }

        public async Task<IReadOnlyList<PaymentVm>> GetByUserIdAsync(string userId, CancellationToken ct = default)
        {
            var payments = await _paymentRepo.GetByUserIdAsync(userId, ct);
            return payments.Select(MapToVm).ToList();
        }

        public async Task<PaymentListVm> GetAllAsync(int pageSize, int pageNo, CancellationToken ct = default)
        {
            var payments = await _paymentRepo.GetPagedAsync(pageSize, pageNo, ct);
            var count = await _paymentRepo.GetCountAsync(ct);
            return new PaymentListVm(payments.Select(MapToVm).ToList(), pageNo, pageSize, count);
        }

        public async Task<PaymentListVm> GetAllUnpaidAsync(int pageSize, int pageNo, CancellationToken ct = default)
        {
            var payments = await _paymentRepo.GetPagedUnpaidAsync(pageSize, pageNo, ct);
            var count = await _paymentRepo.GetUnpaidCountAsync(ct);
            return new PaymentListVm(payments.Select(MapToVm).ToList(), pageNo, pageSize, count);
        }

        public async Task<PaymentOperationResult> ConfirmAsync(ConfirmPaymentDto dto, CancellationToken ct = default)
        {
            var payment = await _paymentRepo.GetByIdAsync(dto.PaymentId, ct);
            if (payment is null)
                return PaymentOperationResult.PaymentNotFound;

            if (payment.Status == PaymentStatus.Confirmed)
                return PaymentOperationResult.AlreadyConfirmed;
            if (payment.Status == PaymentStatus.Expired)
                return PaymentOperationResult.AlreadyExpired;
            if (payment.Status == PaymentStatus.Refunded)
                return PaymentOperationResult.AlreadyRefunded;
            if (payment.Status == PaymentStatus.Cancelled)
                return PaymentOperationResult.AlreadyCancelled;

            var @event = payment.Confirm(dto.TransactionRef);
            await _paymentRepo.UpdateAsync(payment, ct);

            await _broker.PublishAsync(new PaymentConfirmed(
                @event.PaymentId,
                @event.OrderId,
                System.Array.Empty<PaymentConfirmedItem>(),
                @event.OccurredAt));

            return PaymentOperationResult.Success;
        }

        public async Task<PaymentOperationResult> ProcessRefundAsync(int orderId, int refundId, CancellationToken ct = default)
        {
            var payment = await _paymentRepo.GetByOrderIdAsync(orderId, ct);
            if (payment is null)
                return PaymentOperationResult.PaymentNotFound;

            if (payment.Status == PaymentStatus.Expired)
                return PaymentOperationResult.AlreadyExpired;
            if (payment.Status == PaymentStatus.Refunded)
                return PaymentOperationResult.AlreadyRefunded;

            payment.Refund(refundId);
            await _paymentRepo.UpdateAsync(payment, ct);

            return PaymentOperationResult.Success;
        }

        private static PaymentVm MapToVm(Payment payment)
            => new(
                payment.Id.Value,
                payment.OrderId.Value,
                payment.TotalAmount,
                payment.CurrencyId,
                payment.Status.ToString(),
                payment.ExpiresAt,
                payment.ConfirmedAt);

        private static PaymentDetailsVm MapToDetailsVm(Payment payment)
            => new(
                payment.Id.Value,
                payment.PaymentId,
                payment.OrderId.Value,
                payment.TotalAmount,
                payment.CurrencyId,
                payment.Status.ToString(),
                payment.ExpiresAt,
                payment.ConfirmedAt,
                payment.TransactionRef,
                payment.UserId);
    }
}
