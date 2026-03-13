using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Sales.Payments.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Fulfillment
{
    public class PaymentRefundApprovedHandlerTests
    {
        private readonly Mock<IPaymentService> _payments;

        public PaymentRefundApprovedHandlerTests()
        {
            _payments = new Mock<IPaymentService>();
        }

        private PaymentRefundApprovedHandler CreateHandler() => new(_payments.Object);

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldCallProcessRefundAsyncWithCorrectParameters()
        {
            var message = new RefundApproved(
                RefundId: 5,
                OrderId: 99,
                Items: new List<RefundApprovedItem> { new(10, 2) },
                OccurredAt: DateTime.UtcNow);

            await CreateHandler().HandleAsync(message);

            _payments.Verify(s => s.ProcessRefundAsync(99, 5, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
