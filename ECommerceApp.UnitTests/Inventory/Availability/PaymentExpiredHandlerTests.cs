using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Payments.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    /// <summary>
    /// Unit tests for <see cref="PaymentExpiredHandler"/> (Inventory BC).
    /// Verifies that the handler delegates to <see cref="IStockService.ReleaseAllHoldsForOrderAsync"/>
    /// and is resilient when no holds exist.
    /// </summary>
    public class PaymentExpiredHandlerTests
    {
        private readonly Mock<IStockService> _stockService = new();

        private PaymentExpiredHandler CreateHandler()
            => new(_stockService.Object, NullLogger<PaymentExpiredHandler>.Instance);

        private static PaymentExpired CreateMessage(int orderId = 1, int paymentId = 10, Guid? correlationId = null)
            => new(PaymentId: paymentId, OrderId: orderId, OccurredAt: DateTime.UtcNow,
                   CorrelationId: correlationId ?? Guid.NewGuid());

        // ── Happy path ────────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_ShouldCallReleaseAllHoldsForOrder()
        {
            var message = CreateMessage(orderId: 7);

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReleaseAllHoldsForOrderAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ShouldPassOrderIdFromMessage()
        {
            var message = CreateMessage(orderId: 42, paymentId: 99);

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReleaseAllHoldsForOrderAsync(42, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.VerifyNoOtherCalls();
        }

        // ── No holds — no-op ──────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_WhenNoHoldsExist_ShouldCompleteWithoutError()
        {
            _stockService
                .Setup(s => s.ReleaseAllHoldsForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // No exception should be thrown when no holds exist
            await CreateHandler().HandleAsync(CreateMessage(), TestContext.Current.CancellationToken);
        }

        // ── CorrelationId passed through message ──────────────────────────────

        [Fact]
        public async Task HandleAsync_WithSpecificCorrelationId_ShouldNotDropIt()
        {
            var correlationId = Guid.NewGuid();
            var message = CreateMessage(orderId: 5, correlationId: correlationId);

            // The handler doesn't return CorrelationId, but it must not throw — the id
            // travels through the message and is visible in logs (ILogger). This test
            // simply ensures the handler accepts a non-default CorrelationId without error.
            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReleaseAllHoldsForOrderAsync(5, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── Idempotency (second fire is a no-op in IStockService) ─────────────

        [Fact]
        public async Task HandleAsync_CalledTwiceForSameOrder_ShouldDelegateToServiceBothTimes()
        {
            // The handler itself is stateless — idempotency is enforced by IStockService.
            // We verify the handler does NOT suppress the second call (no internal guard).
            var message = CreateMessage(orderId: 3);

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);
            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReleaseAllHoldsForOrderAsync(3, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
