using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Sagas;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.CrossBC
{
    public class AvailabilityReservationChangeSagaCompletionTests
        : BcBaseTest<IMessageBroker>, IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public AvailabilityReservationChangeSagaCompletionTests(
            ITestOutputHelper output,
            MessageProcessingOperationsFixture messageProcessing) : base(output)
        {
            _messageProcessing = messageProcessing;
        }

        private async Task<OutboxStatus?> GetDroppedMessageStatusAsync(CancellationToken cancellationToken)
        {
            var key = MessageTypeRegistry.KeyFor(typeof(CheckoutReservationAvailabilityDropped));
            var row = await GetRequiredService<MessagingDbContext>().Outbox
                .AsNoTracking()
                .Where(outboxMessage => outboxMessage.MessageTypeKey == key)
                .SingleOrDefaultAsync(cancellationToken);
            return row?.Status;
        }

        private sealed class DroppedMessageDispatchedOperation : IMessageProcessingOperation<OutboxStatus?>
        {
            private readonly AvailabilityReservationChangeSagaCompletionTests _test;

            public DroppedMessageDispatchedOperation(AvailabilityReservationChangeSagaCompletionTests test)
            {
                _test = test;
            }

            public Task<OutboxStatus?> ReadAsync(CancellationToken cancellationToken)
            {
                return _test.GetDroppedMessageStatusAsync(cancellationToken);
            }

            public bool IsCompleted(OutboxStatus? state)
            {
                return state == OutboxStatus.Dispatched;
            }

            public string Describe(OutboxStatus? state)
            {
                return $"CheckoutReservationAvailabilityDropped outbox row has status {state?.ToString() ?? "<missing>"}, expected Dispatched.";
            }
        }

        [Fact]
        public async Task StockAvailabilityChanged_CompletesSagaAndRemovesNewestReservationsUntilCapacityFits()
        {
            var reservations = GetRequiredService<ISoftReservationRepository>();
            var oldest = SoftReservation.Create(42, "oldest-holder", 2, 10m, DateTime.UtcNow.AddMinutes(10));
            var newest = SoftReservation.Create(42, "newest-holder", 2, 10m, DateTime.UtcNow.AddMinutes(20));
            await reservations.AddAsync(oldest, CancellationToken);
            await reservations.AddAsync(newest, CancellationToken);

            await PublishAsync(new StockAvailabilityChanged(42, 2, DateTime.UtcNow), CancellationToken);

            var saga = await GetRequiredService<SagasDbContext>().Sagas
                .AsNoTracking()
                .SingleAsync(
                    instance => instance.SagaType == "AvailabilityReservationChange"
                        && instance.CorrelationId == "42",
                    CancellationToken);
            saga.Status.ShouldBe(SagaInstanceStatus.Completed);

            // The running OutboxPollerService delivers the saga's dropped row on its own. Publishing it
            // by hand as well would run InvalidateExcessForProductAsync twice concurrently and the loser
            // fails deleting an already-deleted reservation — so wait for the poller instead.
            await _messageProcessing.WaitUntilAsync(new DroppedMessageDispatchedOperation(this));

            var remaining = await reservations.GetByProductIdAsync(42, CancellationToken);
            remaining.ShouldHaveSingleItem();
            remaining[0].UserId.Value.ShouldBe("oldest-holder");
            remaining[0].Quantity.Value.ShouldBe(2);
        }
    }
}