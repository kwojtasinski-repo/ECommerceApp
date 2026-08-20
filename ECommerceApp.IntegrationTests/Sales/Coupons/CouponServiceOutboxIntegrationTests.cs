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
    public class CouponServiceOutboxIntegrationTests
        : BcBaseTest<ICouponService>, IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public CouponServiceOutboxIntegrationTests(
            ITestOutputHelper output,
            MessageProcessingOperationsFixture messageProcessing) : base(output)
        {
            _messageProcessing = messageProcessing;
        }

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

        // Read order through a fresh scope each call to avoid stale tracked instances
        private async Task<int?> GetOrderCouponUsedIdAsync(
            int orderId,
            CancellationToken cancellationToken)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var order = await repo.GetByIdWithItemsAsync(orderId, cancellationToken);
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
            var couponUsedIdAfterDispatch = await _messageProcessing.WaitUntilAsync(
                new OrderCouponRemovedOperation(this, orderId));

            couponUsedIdAfterDispatch.ShouldBeNull();
        }

        private sealed class OrderCouponRemovedOperation
            : IMessageProcessingOperation<int?>
        {
            private readonly CouponServiceOutboxIntegrationTests _test;
            private readonly int _orderId;

            public OrderCouponRemovedOperation(
                CouponServiceOutboxIntegrationTests test,
                int orderId)
            {
                _test = test;
                _orderId = orderId;
            }

            public Task<int?> ReadAsync(CancellationToken cancellationToken)
            {
                return _test.GetOrderCouponUsedIdAsync(_orderId, cancellationToken);
            }

            public bool IsCompleted(int? state)
            {
                return state is null;
            }

            public string Describe(int? state)
            {
                return $"CouponUsedId for order {_orderId} is still {state?.ToString() ?? "null"}.";
            }
        }
    }
}
