using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Sagas;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.CrossBC
{
    public class AvailabilityReservationChangeSagaCompletionTests : BcBaseTest<IMessageBroker>
    {
        public AvailabilityReservationChangeSagaCompletionTests(ITestOutputHelper output) : base(output) { }

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

            var message = await GetRequiredService<MessagingDbContext>().Outbox
                .AsNoTracking()
                .Where(outboxMessage => outboxMessage.MessageTypeKey ==
                    MessageTypeRegistry.KeyFor(typeof(CheckoutReservationAvailabilityDropped)))
                .Select(outboxMessage => outboxMessage.Payload)
                .SingleAsync(CancellationToken);
            var dropped = JsonSerializer.Deserialize<CheckoutReservationAvailabilityDropped>(message)!;
            await PublishAsync(dropped, CancellationToken);

            var remaining = await reservations.GetByProductIdAsync(42, CancellationToken);
            remaining.ShouldHaveSingleItem();
            remaining[0].UserId.Value.ShouldBe("oldest-holder");
            remaining[0].Quantity.Value.ShouldBe(2);
        }
    }
}