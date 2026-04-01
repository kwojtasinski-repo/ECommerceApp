using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class SnapshotOrderItemsJobTests
    {
        private readonly Mock<IOrderItemRepository> _orderItemRepo;
        private readonly Mock<IOrderProductResolver> _productResolver;

        public SnapshotOrderItemsJobTests()
        {
            _orderItemRepo = new Mock<IOrderItemRepository>();
            _productResolver = new Mock<IOrderProductResolver>();
        }

        private SnapshotOrderItemsJob CreateJob() => new(_orderItemRepo.Object, _productResolver.Object);

        private static JobExecutionContext CreateContext() =>
            new(null, Guid.NewGuid().ToString());

        private static OrderItem CreateOrderItem(int productId = 10)
            => OrderItem.Create(new OrderProductId(productId), 1, new UnitCost(9.99m), new OrderUserId("user-1"));

        // ── ExecuteAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_NoItems_ShouldReportSuccessNoOp()
        {
            _orderItemRepo
                .Setup(r => r.GetUnsnapshottedOrderItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem>());
            var context = CreateContext();

            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _productResolver.Verify(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(It.IsAny<IReadOnlyList<(int, OrderProductSnapshot)>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ItemsWithResolvedSnapshots_ShouldCallSetSnapshotsAndReportSuccess()
        {
            var item1 = CreateOrderItem(productId: 10);
            var item2 = CreateOrderItem(productId: 20);
            var snapshot1 = new OrderProductSnapshot("Product A", "a.jpg", "/api/images/1");
            var snapshot2 = new OrderProductSnapshot("Product B", null, null);

            _orderItemRepo
                .Setup(r => r.GetUnsnapshottedOrderItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem> { item1, item2 });
            _productResolver
                .Setup(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, OrderProductSnapshot> { [10] = snapshot1, [20] = snapshot2 });

            var context = CreateContext();
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(
                It.Is<IReadOnlyList<(int, OrderProductSnapshot)>>(l => l.Count == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ProductNotFound_ShouldSkipItemAndStillSetOthers()
        {
            var item1 = CreateOrderItem(productId: 10);
            var item2 = CreateOrderItem(productId: 99);
            var snapshot1 = new OrderProductSnapshot("Product A", "a.jpg", "/api/images/1");

            _orderItemRepo
                .Setup(r => r.GetUnsnapshottedOrderItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem> { item1, item2 });
            _productResolver
                .Setup(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, OrderProductSnapshot> { [10] = snapshot1 });

            var context = CreateContext();
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(
                It.Is<IReadOnlyList<(int, OrderProductSnapshot)>>(l => l.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_AllProductsNotFound_ShouldNotCallSetSnapshotsAndReportSuccess()
        {
            var item1 = CreateOrderItem(productId: 99);

            _orderItemRepo
                .Setup(r => r.GetUnsnapshottedOrderItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem> { item1 });
            _productResolver
                .Setup(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, OrderProductSnapshot>());

            var context = CreateContext();
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(It.IsAny<IReadOnlyList<(int, OrderProductSnapshot)>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
