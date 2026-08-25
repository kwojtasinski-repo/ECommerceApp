using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Shared.TestInfrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Supporting.Communication
{
    public class PaymentConfirmedEmailHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public PaymentConfirmedEmailHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SamePaymentConfirmed_ShouldSendEmailExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new PaymentConfirmed(PaymentId: 1, OrderId: orderId, Items: new List<PaymentConfirmedItem>(), OccurredAt: DateTime.UtcNow);

            await AssertEmailSentOnceAsync(message, outboxMessageId: 940001, CancellationToken);
        }
    }

    public class PaymentConfirmedNotificationHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public PaymentConfirmedNotificationHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SamePaymentConfirmed_ShouldNotifyExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new PaymentConfirmed(PaymentId: 1, OrderId: orderId, Items: new List<PaymentConfirmedItem>(), OccurredAt: DateTime.UtcNow);

            await AssertNotifiedOnceAsync(message, outboxMessageId: 940002, CancellationToken);
        }
    }

    public class PaymentExpiredEmailHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public PaymentExpiredEmailHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SamePaymentExpired_ShouldSendEmailExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new PaymentExpired(PaymentId: 1, OrderId: orderId, OccurredAt: DateTime.UtcNow);

            await AssertEmailSentOnceAsync(message, outboxMessageId: 940003, CancellationToken);
        }
    }

    public class PaymentExpiredNotificationHandlerDuplicateDeliveryTests : CommunicationDuplicateDeliveryTestBase
    {
        public PaymentExpiredNotificationHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RedeliverAsync_SamePaymentExpired_ShouldNotifyExactlyOnce()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var message = new PaymentExpired(PaymentId: 1, OrderId: orderId, OccurredAt: DateTime.UtcNow);

            await AssertNotifiedOnceAsync(message, outboxMessageId: 940004, CancellationToken);
        }
    }
}
