using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
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

            await RedeliverAsync(message, outboxMessageId: 940009, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 940009, CancellationToken);

            GetRequiredService<CountingEmailService>().SentCount.ShouldBe(1);
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

            await RedeliverAsync(message, outboxMessageId: 940010, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 940010, CancellationToken);

            GetRequiredService<CountingNotificationService>().NotifyCount.ShouldBe(1);
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

            await RedeliverAsync(message, outboxMessageId: 940011, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 940011, CancellationToken);

            GetRequiredService<CountingEmailService>().SentCount.ShouldBe(1);
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

            await RedeliverAsync(message, outboxMessageId: 940012, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 940012, CancellationToken);

            GetRequiredService<CountingNotificationService>().NotifyCount.ShouldBe(1);
        }
    }
}
