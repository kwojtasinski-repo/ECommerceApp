using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.Infrastructure.Sagas;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.CrossBC
{
    public class CartRecoverySagaCompletionTests : BcBaseTest<IMessageBroker>
    {
        public CartRecoverySagaCompletionTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task CheckoutReservationRevertRequested_CompletesCartRecoverySaga()
        {
            const string userId = "cart-recovery-user";

            await PublishAsync(
                new CheckoutReservationRevertRequested(userId),
                CancellationToken);

            var saga = await GetRequiredService<SagasDbContext>().Sagas
                .AsNoTracking()
                .SingleAsync(
                    instance => instance.SagaType == "CartRecovery"
                        && instance.CorrelationId == userId,
                    CancellationToken);

            saga.Status.ShouldBe(SagaInstanceStatus.Completed);
        }
    }
}