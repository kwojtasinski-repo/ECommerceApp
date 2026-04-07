using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Shared;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderPlacedSnapshotHandlerTests
    {
        private readonly Mock<IOrderItemRepository> _orderItemRepo;
        private readonly Mock<IOrderProductResolver> _productResolver;

        public OrderPlacedSnapshotHandlerTests()
        {
            _orderItemRepo = new Mock<IOrderItemRepository>();
            _productResolver = new Mock<IOrderProductResolver>();
        }

        private OrderPlacedSnapshotHandler CreateHandler()
            => new(_orderItemRepo.Object, _productResolver.Object);

        private static OrderPlaced CreateMessage(int orderId = 1)
            => new(orderId, new List<OrderPlacedItem>(), "user-1", DateTime.UtcNow.AddDays(3), DateTime.UtcNow, 100m, 1);

        private static OrderItem CreateOrderItem(int productId = 10)
            => OrderItem.Create(new OrderProductId(productId), 1, new UnitCost(9.99m), new OrderUserId("user-1"));

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_NoItemsForOrder_ShouldNotCallSetSnapshotsAsync()
        {
            _orderItemRepo
                .Setup(r => r.GetByOrderIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem>());

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), CancellationToken.None);

            _productResolver.Verify(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(It.IsAny<IReadOnlyList<(int, OrderProductSnapshot)>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_AllProductsResolved_ShouldCallSetSnapshotsWithAllItems()
        {
            var item1 = CreateOrderItem(productId: 10);
            var item2 = CreateOrderItem(productId: 20);
            var snapshot1 = new OrderProductSnapshot("Product A", "a.jpg", 1);
            var snapshot2 = new OrderProductSnapshot("Product B", null, null);

            _orderItemRepo
                .Setup(r => r.GetByOrderIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem> { item1, item2 });
            _productResolver
                .Setup(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, OrderProductSnapshot> { [10] = snapshot1, [20] = snapshot2 });

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), CancellationToken.None);

            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(
                It.Is<IReadOnlyList<(int, OrderProductSnapshot)>>(l => l.Count == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SomeProductsNotFound_ShouldCallSetSnapshotsWithResolvedOnly()
        {
            var item1 = CreateOrderItem(productId: 10);
            var item2 = CreateOrderItem(productId: 99);
            var snapshot1 = new OrderProductSnapshot("Product A", "a.jpg", 1);

            _orderItemRepo
                .Setup(r => r.GetByOrderIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem> { item1, item2 });
            _productResolver
                .Setup(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, OrderProductSnapshot> { [10] = snapshot1 });

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), CancellationToken.None);

            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(
                It.Is<IReadOnlyList<(int, OrderProductSnapshot)>>(l => l.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_AllProductsNotFound_ShouldNotCallSetSnapshotsAsync()
        {
            var item1 = CreateOrderItem(productId: 99);

            _orderItemRepo
                .Setup(r => r.GetByOrderIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem> { item1 });
            _productResolver
                .Setup(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, OrderProductSnapshot>());

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), CancellationToken.None);

            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(It.IsAny<IReadOnlyList<(int, OrderProductSnapshot)>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
