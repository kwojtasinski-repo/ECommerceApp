using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ECommerceApp.E2E.Backend.Sales.Orders
{
    /// <summary>
    /// Release-blocking proof required by the Phase 3 Outbox-retrofit validation checklist
    /// (<c>.github/plans/03-phase-outbox-retrofit-callsites-validation.md</c>, checklist item
    /// "the OrderService.PlaceOrderAsync rollback-atomicity test"): a failure occurring after the
    /// Order aggregate write but before the Outbox commit must roll back BOTH, leaving no orphaned
    /// Order/OrderItem rows behind. Runs against a real SQL Server engine (via
    /// <see cref="OrderRollbackE2EFixture"/>/Testcontainers) because EF Core's InMemory provider
    /// (used by <c>ECommerceApp.IntegrationTests</c>) cannot exercise real transaction rollback at all.
    /// </summary>
    [Collection("OrderRollbackSqlServer")]
    public class OrderServiceRollbackE2ETests : IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly ITestOutputHelper _output;

        public OrderServiceRollbackE2ETests(OrderRollbackE2EFixture fixture, ITestOutputHelper output)
        {
            _scope = fixture.Services.CreateScope();
            _output = output;
        }

        public void Dispose() => _scope.Dispose();

        private T GetRequiredService<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

        private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

        private async Task<int> SeedCustomerAsync()
        {
            var repo = GetRequiredService<IUserProfileRepository>();
            var profile = UserProfile.Create(
                userId: Guid.NewGuid().ToString(),
                firstName: "Jan",
                lastName: "Kowalski",
                isCompany: false,
                nip: null,
                companyName: null,
                email: $"{Guid.NewGuid():N}@test.com",
                phoneNumber: "123456789");
            // flatNumber intentionally non-null: OrderCustomerResolver.ResolveAsync currently throws
            // a NullReferenceException on `address.FlatNumber.Value` when an address has no flat
            // number (pre-existing bug, unrelated to the Outbox retrofit — reported separately rather
            // than silently patched here).
            profile.AddAddress("Główna", "1", 1, "67-100", "Nowa Sól", "PL");
            var id = await repo.AddAsync(profile);
            return id.Value;
        }

        private async Task<int> SeedCartItemAsync(string userId)
        {
            var repo = GetRequiredService<IOrderItemRepository>();
            var item = OrderItem.Create(new OrderProductId(Random.Shared.Next(1, int.MaxValue)), 2, new UnitCost(10m), userId);
            return await repo.AddAsync(item, CancellationToken);
        }

        [Fact]
        public async Task PlaceOrderAsync_FailureBetweenOrderWriteAndOutboxCommit_RollsBackOrderAndOrderItemAssignment()
        {
            var userId = $"rollback-e2e-{Guid.NewGuid():N}";
            var customerId = await SeedCustomerAsync();
            var cartItemId = await SeedCartItemAsync(userId);

            var service = GetRequiredService<IOrderService>();
            var dto = new PlaceOrderDto(customerId, CurrencyId: 1, UserId: userId, CartItemIds: new List<int> { cartItemId });

            // AssignToOrderThrowingRepository (wired in OrderRollbackE2EWebApplicationFactory) throws
            // from AssignToOrderAsync, which runs after Order.AddAsync but before the Outbox commit —
            // exactly the window this test targets.
            var ex = await Record.ExceptionAsync(() => service.PlaceOrderAsync(dto, CancellationToken));
            _output.WriteLine(ex?.ToString() ?? "(no exception thrown)");
            ex.ShouldNotBeNull();
            ex.ShouldBeOfType<InvalidOperationException>();
            ex.Message.ShouldContain("Simulated failure");

            // The Order write must have been rolled back along with the doomed Outbox enqueue —
            // querying by UserId (not by a guessed id) proves no Order row was left behind at all.
            var orderRepo = GetRequiredService<IOrderRepository>();
            var orders = await orderRepo.GetByUserIdAsync(userId, CancellationToken);
            orders.ShouldBeEmpty();

            // The cart item must still be an unassigned cart item (OrderId rolled back to null),
            // not left dangling as attached to a since-rolled-back order.
            var itemRepo = GetRequiredService<IOrderItemRepository>();
            var cartItem = await itemRepo.GetByIdAsync(cartItemId, CancellationToken);
            cartItem.ShouldNotBeNull();
            cartItem.OrderId.ShouldBeNull();
        }
    }
}
