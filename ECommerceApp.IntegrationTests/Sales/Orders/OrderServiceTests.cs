using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Orders
{
    /// <summary>
    /// Integration tests for the new Sales/Orders BC <see cref="IOrderService"/>.
    ///
    /// Infrastructure notes:
    /// - All DbContexts (including <c>OrdersDbContext</c>) use InMemory databases via
    ///   <see cref="BcBaseTest{T}"/> — no SQL Server dependency.
    /// - Guard-condition tests return before touching the DB; query tests operate on an
    ///   empty InMemory store and assert null / empty / not-found results.
    /// </summary>
    public class OrderServiceTests : BcBaseTest<IOrderService>
    {
        public OrderServiceTests(ITestOutputHelper output) : base(output) { }

        // ── PlaceOrderAsync — guard conditions (InMemory-safe) ────────────────

        [Fact]
        public async Task PlaceOrderAsync_NonExistentCustomer_ShouldReturnCustomerNotFound()
        {
            // CustomerExistenceChecker queries the in-memory Context (empty) →
            // returns false → PlaceOrderResult.CustomerNotFound before hitting OrdersDbContext
            var dto = new PlaceOrderDto(
                CustomerId: 99999,
                CurrencyId: 1,
                UserId: PROPER_CUSTOMER_ID,
                CartItemIds: new List<int> { 1 });

            var result = await _service.PlaceOrderAsync(dto, CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.CustomerId.ShouldBe(99999);
            result.FailureReason.ShouldContain("99999");
        }

        // ── GetOrderDetailsAsync — requires OrdersDbContext (SQL Server) ──────

        [Fact]
        public async Task GetOrderDetailsAsync_NonExistentOrder_ShouldReturnNull()
        {
            var result = await _service.GetOrderDetailsAsync(int.MaxValue, CancellationToken);

            result.ShouldBeNull();
        }

        // ── GetAllOrdersAsync — requires OrdersDbContext (SQL Server) ─────────

        [Fact]
        public async Task GetAllOrdersAsync_ValidPagination_ShouldReturnPagedResult()
        {
            var result = await _service.GetAllOrdersAsync(pageSize: 10, pageNo: 1, search: null, CancellationToken);

            result.ShouldNotBeNull();
            result.PageSize.ShouldBe(10);
            result.CurrentPage.ShouldBe(1);
            result.Orders.ShouldNotBeNull();
        }

        // ── GetOrdersByUserIdAsync — requires OrdersDbContext (SQL Server) ────

        [Fact]
        public async Task GetOrdersByUserIdAsync_NonExistentUser_ShouldReturnEmptyList()
        {
            var result = await _service.GetOrdersByUserIdAsync("non-existent-user-id", CancellationToken);

            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }

        // ── MarkAsDeliveredAsync — guard conditions ───────────────────────────

        [Fact]
        public async Task MarkAsDeliveredAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var result = await _service.MarkAsDeliveredAsync(int.MaxValue, CancellationToken);

            result.ShouldBe(OrderOperationResult.OrderNotFound);
        }

        // ── DeleteOrderAsync — guard conditions ───────────────────────────────

        [Fact]
        public async Task DeleteOrderAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var result = await _service.DeleteOrderAsync(int.MaxValue, CancellationToken);

            result.ShouldBe(OrderOperationResult.OrderNotFound);
        }

        // ── RemoveCouponAsync — guard conditions ──────────────────────────────

        [Fact]
        public async Task RemoveCouponAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var result = await _service.RemoveCouponAsync(int.MaxValue, CancellationToken);

            result.ShouldBe(OrderOperationResult.OrderNotFound);
        }

        // ── GetCustomerIdAsync — guard conditions ─────────────────────────────

        [Fact]
        public async Task GetCustomerIdAsync_NonExistentOrder_ShouldReturnNull()
        {
            var result = await _service.GetCustomerIdAsync(int.MaxValue, CancellationToken);

            result.ShouldBeNull();
        }
    }
}

