using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Payments
{
    public class PaymentServiceTests : BcBaseTest<IPaymentService>
    {
        public PaymentServiceTests(ITestOutputHelper output) : base(output) { }

        // ── Helpers ───────────────────────────────────────────────────────

        private async Task<int> SeedPaymentViaOrderPlacedAsync(CancellationToken ct = default,
            int orderId = 1,
            decimal totalAmount = 100m,
            int currencyId = 1)
        {
            var orderPlaced = new OrderPlaced(
                OrderId: orderId,
                Items: new List<OrderPlacedItem> { new(ProductId: 10, Quantity: 2) },
                UserId: PROPER_CUSTOMER_ID,
                ExpiresAt: DateTime.UtcNow.AddHours(24),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: totalAmount,
                CurrencyId: currencyId);

            await PublishAsync(orderPlaced, CancellationToken);
            // OrderPlacedHandler creates a Payment with auto-generated ID (1 for first)
            return orderId;
        }

        // ── GetByIdAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetByIdAsync_NonExistentPayment_ShouldReturnNull()
        {
            var result = await _service.GetByIdAsync(int.MaxValue, CancellationToken);

            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ExistingPayment_ShouldReturnPaymentDetails()
        {
            var orderId = await SeedPaymentViaOrderPlacedAsync(ct: CancellationToken);

            var result = await _service.GetByOrderIdAsync(orderId, CancellationToken);

            result.ShouldNotBeNull();
            var payment = await _service.GetByIdAsync(result.Id, CancellationToken);

            payment.ShouldNotBeNull();
            payment.OrderId.ShouldBe(orderId);
            payment.TotalAmount.ShouldBe(100m);
            payment.CurrencyId.ShouldBe(1);
            payment.Status.ShouldBe("Pending");
        }

        // ── GetByOrderIdAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetByOrderIdAsync_NonExistentOrder_ShouldReturnNull()
        {
            var result = await _service.GetByOrderIdAsync(int.MaxValue, CancellationToken);

            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetByOrderIdAsync_ExistingPayment_ShouldReturnPaymentDetails()
        {
            var orderId = await SeedPaymentViaOrderPlacedAsync(orderId: 42, totalAmount: 250.50m, currencyId: 2, ct: CancellationToken);

            var result = await _service.GetByOrderIdAsync(42, CancellationToken);

            result.ShouldNotBeNull();
            result.OrderId.ShouldBe(42);
            result.TotalAmount.ShouldBe(250.50m);
            result.CurrencyId.ShouldBe(2);
            result.Status.ShouldBe("Pending");
            result.ConfirmedAt.ShouldBeNull();
        }

        // ── ConfirmAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmAsync_NonExistentPayment_ShouldReturnPaymentNotFound()
        {
            var result = await _service.ConfirmAsync(new ConfirmPaymentDto(int.MaxValue, "TX-001"), CancellationToken);

            result.ShouldBe(PaymentOperationResult.PaymentNotFound);
        }

        [Fact]
        public async Task ConfirmAsync_PendingPayment_ShouldReturnSuccess()
        {
            var orderId = await SeedPaymentViaOrderPlacedAsync(ct: CancellationToken);
            var payment = await _service.GetByOrderIdAsync(orderId, CancellationToken);

            var result = await _service.ConfirmAsync(new ConfirmPaymentDto(payment!.Id, "TX-CONFIRM"), CancellationToken);

            result.ShouldBe(PaymentOperationResult.Success);

            var confirmed = await _service.GetByIdAsync(payment.Id, CancellationToken);
            confirmed.ShouldNotBeNull();
            confirmed.Status.ShouldBe("Confirmed");
            confirmed.TransactionRef.ShouldBe("TX-CONFIRM");
            confirmed.ConfirmedAt.ShouldNotBeNull();
        }

        [Fact]
        public async Task ConfirmAsync_AlreadyConfirmedPayment_ShouldReturnAlreadyConfirmed()
        {
            var orderId = await SeedPaymentViaOrderPlacedAsync(ct: CancellationToken);
            var payment = await _service.GetByOrderIdAsync(orderId, CancellationToken);
            await _service.ConfirmAsync(new ConfirmPaymentDto(payment!.Id, "TX-1"), CancellationToken);

            var result = await _service.ConfirmAsync(new ConfirmPaymentDto(payment.Id, "TX-2"), CancellationToken);

            result.ShouldBe(PaymentOperationResult.AlreadyConfirmed);
        }

        // ── ProcessRefundAsync ───────────────────────────────────────────

        [Fact]
        public async Task ProcessRefundAsync_NonExistentPayment_ShouldReturnPaymentNotFound()
        {
            var result = await _service.ProcessRefundAsync(orderId: int.MaxValue, refundId: 1, CancellationToken);

            result.ShouldBe(PaymentOperationResult.PaymentNotFound);
        }

        [Fact]
        public async Task ProcessRefundAsync_ConfirmedPayment_ShouldReturnSuccess()
        {
            var orderId = await SeedPaymentViaOrderPlacedAsync(ct: CancellationToken);
            var payment = await GetRequiredService<IPaymentService>().GetByOrderIdAsync(orderId, CancellationToken);
            await GetRequiredService<IPaymentService>().ConfirmAsync(new ConfirmPaymentDto(payment!.Id, "TX-REF"), CancellationToken);

            var result = await GetRequiredService<IPaymentService>().ProcessRefundAsync(orderId, refundId: 99, CancellationToken);

            result.ShouldBe(PaymentOperationResult.Success);

            var refunded = await GetRequiredService<IPaymentService>().GetByOrderIdAsync(orderId, CancellationToken);
            refunded.ShouldNotBeNull();
            refunded.Status.ShouldBe("Refunded");
        }

        [Fact]
        public async Task ProcessRefundAsync_AlreadyRefundedPayment_ShouldReturnAlreadyRefunded()
        {
            var orderId = await SeedPaymentViaOrderPlacedAsync(ct: CancellationToken);
            var payment = await GetRequiredService<IPaymentService>().GetByOrderIdAsync(orderId, CancellationToken);
            await GetRequiredService<IPaymentService>().ConfirmAsync(new ConfirmPaymentDto(payment!.Id, "TX-1"), CancellationToken);
            await GetRequiredService<IPaymentService>().ProcessRefundAsync(orderId, refundId: 1, CancellationToken);

            var result = await GetRequiredService<IPaymentService>().ProcessRefundAsync(orderId, refundId: 2, CancellationToken);

            result.ShouldBe(PaymentOperationResult.AlreadyRefunded);
        }
    }
}

