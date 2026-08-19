using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Shared.TestInfrastructure.TestData;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Supporting.Communication
{
    public class OrderPlacedEmailHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public OrderPlacedEmailHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SameOrderPlaced_ShouldSendEmailExactlyOnce()
        {
            var message = OrderPlacedTestData.Create(
                totalAmount: 100m,
                userId: PROPER_CUSTOMER_ID,
                expirationHours: 1,
                includeItem: false);

            await RedeliverAsync(message, outboxMessageId: 940005, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 940005, CancellationToken);

            GetRequiredService<CountingEmailService>().SentCount.ShouldBe(1);
        }
    }

    public class OrderPlacedNotificationHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public OrderPlacedNotificationHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SameOrderPlaced_ShouldNotifyExactlyOnce()
        {
            var message = OrderPlacedTestData.Create(
                totalAmount: 100m,
                userId: PROPER_CUSTOMER_ID,
                expirationHours: 1,
                includeItem: false);

            await RedeliverAsync(message, outboxMessageId: 940006, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 940006, CancellationToken);

            GetRequiredService<CountingNotificationService>().NotifyCount.ShouldBe(1);
        }
    }

    public class OrderCancelledEmailHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public OrderCancelledEmailHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SameOrderCancelled_ShouldSendEmailExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new OrderCancelled(
                OrderId: orderId,
                Items: new List<OrderCancelledItem>(),
                OccurredAt: DateTime.UtcNow);

            await RedeliverAsync(message, outboxMessageId: 940007, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 940007, CancellationToken);

            GetRequiredService<CountingEmailService>().SentCount.ShouldBe(1);
        }
    }

    public class OrderCancelledNotificationHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public OrderCancelledNotificationHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SameOrderCancelled_ShouldNotifyExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new OrderCancelled(
                OrderId: orderId,
                Items: new List<OrderCancelledItem>(),
                OccurredAt: DateTime.UtcNow);

            await RedeliverAsync(message, outboxMessageId: 940008, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 940008, CancellationToken);

            GetRequiredService<CountingNotificationService>().NotifyCount.ShouldBe(1);
        }
    }
}
