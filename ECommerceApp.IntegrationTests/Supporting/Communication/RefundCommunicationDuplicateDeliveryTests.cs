using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Shared.TestInfrastructure.TestData;
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
            var message = RefundApprovedTestData.Create(orderId, includeItem: false);

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
            var message = RefundApprovedTestData.Create(orderId, includeItem: false);

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
            var message = RefundRejectedTestData.Create(orderId);

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
            var message = RefundRejectedTestData.Create(orderId);

            await AssertNotifiedOnceAsync(message, outboxMessageId: 940012, CancellationToken);
        }
    }
}
