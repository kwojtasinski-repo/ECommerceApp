using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Shared.TestInfrastructure;
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
            var message = new OrderPlaced(
                OrderId: 1,
                Items: new List<OrderPlacedItem>(),
                UserId: PROPER_CUSTOMER_ID,
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 100m,
                CurrencyId: 1);

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
            var message = new OrderPlaced(
                OrderId: 1,
                Items: new List<OrderPlacedItem>(),
                UserId: PROPER_CUSTOMER_ID,
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 100m,
                CurrencyId: 1);

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
