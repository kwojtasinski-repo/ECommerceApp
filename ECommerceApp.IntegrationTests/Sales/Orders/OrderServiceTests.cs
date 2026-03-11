using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.IntegrationTests.Common;
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
    /// - The legacy <c>Context</c> is replaced with an InMemory database (empty — no seed data).
    /// - <c>OrdersDbContext</c> uses the real SQL Server connection string from
    ///   appsettings.json / appsettings.docker.json. Tests marked [RequiresSqlServer] below
    ///   will fail if the <c>sales.*</c> schema does not exist. Run
    ///   <c>dotnet ef database update --context OrdersDbContext</c> before those tests.
    /// - Guard-condition tests (CustomerNotFound, CartItemsNotFound) return before hitting
    ///   OrdersDbContext and work in all environments.
    /// </summary>
    public class OrderServiceTests : BaseTest<IOrderService>
    {
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

            var result = await _service.PlaceOrderAsync(dto);

            result.IsSuccess.ShouldBeFalse();
            result.CustomerId.ShouldBe(99999);
            result.FailureReason.ShouldContain("99999");
        }

        // ── GetOrderDetailsAsync — requires OrdersDbContext (SQL Server) ──────

        [Fact]
        public async Task GetOrderDetailsAsync_NonExistentOrder_ShouldReturnNull()
        {
            var result = await _service.GetOrderDetailsAsync(int.MaxValue);

            result.ShouldBeNull();
        }

        // ── GetAllOrdersAsync — requires OrdersDbContext (SQL Server) ─────────

        [Fact]
        public async Task GetAllOrdersAsync_ValidPagination_ShouldReturnPagedResult()
        {
            var result = await _service.GetAllOrdersAsync(pageSize: 10, pageNo: 1, search: null);

            result.ShouldNotBeNull();
            result.PageSize.ShouldBe(10);
            result.CurrentPage.ShouldBe(1);
            result.Orders.ShouldNotBeNull();
        }

        // ── GetOrdersByUserIdAsync — requires OrdersDbContext (SQL Server) ────

        [Fact]
        public async Task GetOrdersByUserIdAsync_NonExistentUser_ShouldReturnEmptyList()
        {
            var result = await _service.GetOrdersByUserIdAsync("non-existent-user-id");

            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }

        // ── MarkAsDeliveredAsync — guard conditions ───────────────────────────

        [Fact]
        public async Task MarkAsDeliveredAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var result = await _service.MarkAsDeliveredAsync(int.MaxValue);

            result.ShouldBe(OrderOperationResult.OrderNotFound);
        }

        // ── DeleteOrderAsync — guard conditions ───────────────────────────────

        [Fact]
        public async Task DeleteOrderAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var result = await _service.DeleteOrderAsync(int.MaxValue);

            result.ShouldBe(OrderOperationResult.OrderNotFound);
        }

        // ── RemoveCouponAsync — guard conditions ──────────────────────────────

        [Fact]
        public async Task RemoveCouponAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var result = await _service.RemoveCouponAsync(int.MaxValue);

            result.ShouldBe(OrderOperationResult.OrderNotFound);
        }

        // ── GetCustomerIdAsync — guard conditions ─────────────────────────────

        [Fact]
        public async Task GetCustomerIdAsync_NonExistentOrder_ShouldReturnNull()
        {
            var result = await _service.GetCustomerIdAsync(int.MaxValue);

            result.ShouldBeNull();
        }
    }
}
