using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Coupons
{
    public class CouponServiceOutboxIntegrationTests : BcBaseTest<ICouponService>
    {
        public CouponServiceOutboxIntegrationTests(ITestOutputHelper output) : base(output) { }

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private async Task<int> SeedOrderAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order);
        }

        private async Task<CouponId> SeedCouponAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<ICouponRepository>();
            var code = "SAVE-E2E-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var coupon = Coupon.Create(code, "e2e test coupon");
            await repo.AddAsync(coupon, ct);
            var seeded = await repo.GetByCodeAsync(code, ct);
            // Ensure the stored coupon is in Used status for the Remove path
            seeded!.MarkAsUsed();
            await repo.UpdateAsync(seeded, ct);
            return seeded.Id;
        }

        private async Task<int> SeedCouponUsedAsync(CouponId couponId, int orderId, CancellationToken ct = default)
        {
            var repo = GetRequiredService<ICouponUsedRepository>();
            var couponUsed = CouponUsed.CreateForDbCoupon(couponId, orderId, "user-1");
            await repo.AddAsync(couponUsed, ct);
            var seeded = await repo.FindByOrderIdAsync(orderId, ct);
            return seeded!.Id.Value;
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition())
                    return;

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        // Read order through a fresh scope each call to avoid stale tracked instances
        private async Task<int?> GetOrderCouponUsedIdAsync(int orderId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var order = await repo.GetByIdWithItemsAsync(orderId, CancellationToken);
            return order?.CouponUsedId;
        }

        [Fact]
        public async Task RemoveCouponAsync_EnqueuesOutboxMessage_AndOrderEventuallyHasNoCoupon()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var couponId = await SeedCouponAsync(CancellationToken);
            var couponUsedId = await SeedCouponUsedAsync(couponId, orderId, CancellationToken);

            // Attach the coupon to the order via OrderService so the Remove path has something to remove
            var orderService = Services.GetRequiredService<IOrderService>();
            var addResult = await orderService.AddCouponAsync(orderId, couponUsedId, 10, CancellationToken);
            addResult.ShouldBe(OrderOperationResult.Success);

            // Sanity check: the coupon should be in Used status now
            var couponRepo = Services.GetRequiredService<ICouponRepository>();
            var c = await couponRepo.GetByIdAsync(couponId.Value, CancellationToken);
            c.ShouldNotBeNull();
            c.Status.ToString().ShouldBe("Used");

            // Verify the CouponUsed record references the expected CouponId
            var couponUsedRepo = Services.GetRequiredService<ICouponUsedRepository>();
            var used = await couponUsedRepo.FindByOrderIdAsync(orderId, CancellationToken);
            used.ShouldNotBeNull();
            used.CouponId.Value.ShouldBe(couponId.Value);

            // Act: call RemoveCouponAsync on the ICouponService (this should enqueue outbox message)
            var result = await _service.RemoveCouponAsync(orderId, CancellationToken);
            result.ShouldBe(CouponRemoveResult.Removed);

            // Poll until the OutboxDispatcher has run and the order no longer has CouponUsedId
            await WaitUntilAsync(async () => await GetOrderCouponUsedIdAsync(orderId) == null, TimeSpan.FromSeconds(20));

            (await GetOrderCouponUsedIdAsync(orderId)).ShouldBeNull();
        }
    }
}
