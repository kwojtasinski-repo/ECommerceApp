using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.Communication.Emails;
using ECommerceApp.Application.Supporting.Communication.Services;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Supporting.Communication
{
    /// <summary>
    /// Shared base for the 12 Communication handlers' Phase 4 <c>DuplicateDeliveryTests</c>. Swaps in
    /// <see cref="CountingEmailService"/>/<see cref="CountingNotificationService"/> so a redelivery test
    /// can assert the handler's unconditional email/notification send happened exactly once, instead of
    /// asserting on DB state (Communication has none of its own — see the plan file's correction on why
    /// these handlers use the transaction-less <c>IProcessedMessageGuard</c> overload).
    /// </summary>
    public abstract class CommunicationDuplicateDeliveryTestBase : BcBaseTest<IMessageBroker>
    {
        protected CommunicationDuplicateDeliveryTestBase(ITestOutputHelper output) : base(output) { }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<CountingEmailService>();
                services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<CountingEmailService>());
                services.AddSingleton<CountingNotificationService>();
                services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<CountingNotificationService>());
            });
        }

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        /// <summary>
        /// Seeds a real Order with <see cref="BcWebApplicationFactory.PROPER_CUSTOMER_ID"/> (a
        /// pre-seeded IAM user with a real email) as its <c>UserId</c> — several Communication handlers
        /// resolve the recipient via <c>IOrderUserResolver.GetUserIdForOrderAsync</c>, which queries the
        /// real Orders BC, not a mock.
        /// </summary>
        protected async Task<int> SeedOrderAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order, ct);
        }
    }
}
