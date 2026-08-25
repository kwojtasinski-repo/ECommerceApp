using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Shared.TestInfrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Supporting.Communication
{
    public class RefundApprovedEmailHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public RefundApprovedEmailHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SameRefundApproved_ShouldSendEmailExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new RefundApproved(
                RefundId: 1,
                OrderId: orderId,
                Items: new List<RefundApprovedItem>(),
                OccurredAt: DateTime.UtcNow);

            await AssertEmailSentOnceAsync(message, outboxMessageId: 940009, CancellationToken);
        }
    }

    public class RefundApprovedNotificationHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public RefundApprovedNotificationHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SameRefundApproved_ShouldNotifyExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new RefundApproved(
                RefundId: 1,
                OrderId: orderId,
                Items: new List<RefundApprovedItem>(),
                OccurredAt: DateTime.UtcNow);

            await AssertNotifiedOnceAsync(message, outboxMessageId: 940010, CancellationToken);
        }
    }

    public class RefundRejectedEmailHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public RefundRejectedEmailHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SameRefundRejected_ShouldSendEmailExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new RefundRejected(
                RefundId: 1,
                OrderId: orderId,
                OccurredAt: DateTime.UtcNow);

            await AssertEmailSentOnceAsync(message, outboxMessageId: 940011, CancellationToken);
        }
    }

    public class RefundRejectedNotificationHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public RefundRejectedNotificationHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SameRefundRejected_ShouldNotifyExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new RefundRejected(
                RefundId: 1,
                OrderId: orderId,
                OccurredAt: DateTime.UtcNow);

            await AssertNotifiedOnceAsync(message, outboxMessageId: 940012, CancellationToken);
        }
    }
}
