using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Sagas;
using ECommerceApp.IntegrationTests.Supporting.Communication;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.CrossBC
{
    public class RefundSagaCompletionTests : CommunicationDuplicateDeliveryTestBase
    {
        public RefundSagaCompletionTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task RefundApproved_WhenCompletionMessagesAreDispatched_CompletesSaga()
        {
            const int refundId = 901;
            var orderId = await SeedOrderAsync(CancellationToken);

            await PublishAsync(
                new RefundApproved(
                    RefundId: refundId,
                    OrderId: orderId,
                    Items: new List<RefundApprovedItem>(),
                    OccurredAt: DateTime.UtcNow),
                CancellationToken);

            var messagingContext = GetRequiredService<MessagingDbContext>();
            var completionMessages = await messagingContext.Outbox
                .AsNoTracking()
                .Where(message => message.MessageTypeKey == MessageTypeRegistry.KeyFor(typeof(RefundStockReturned))
                    || message.MessageTypeKey == MessageTypeRegistry.KeyFor(typeof(RefundCustomerNotified)))
                .OrderBy(message => message.Id)
                .ToListAsync(CancellationToken);

            completionMessages.Count.ShouldBe(2);
            foreach (var outboxMessage in completionMessages)
            {
                var messageType = MessageTypeRegistry.TypeFor(outboxMessage.MessageTypeKey);
                var message = (IMessage)JsonSerializer.Deserialize(
                    outboxMessage.Payload,
                    messageType)!;
                await PublishAsync(message, CancellationToken);
            }

            var saga = await GetRequiredService<SagasDbContext>().Sagas
                .AsNoTracking()
                .SingleAsync(
                    instance => instance.SagaType == "Refund"
                        && instance.CorrelationId == refundId.ToString(),
                    CancellationToken);

            saga.Status.ShouldBe(SagaInstanceStatus.Completed);
        }
    }
}