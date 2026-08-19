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

        private void SetupOrderItems(int orderId, params OrderItem[] items)
        {
            _orderItemRepo
                .Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem>(items));
        }

        private void SetupProductSnapshots(IReadOnlyDictionary<int, OrderProductSnapshot> snapshots)
        {
            _productResolver
                .Setup(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshots);
        }

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_NoItemsForOrder_ShouldNotCallSetSnapshotsAsync()
        {
            // Arrange
            SetupOrderItems(1);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), CancellationToken.None);

            // Assert
            _productResolver.Verify(r => r.ResolveAllAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(It.IsAny<IReadOnlyList<(int, OrderProductSnapshot)>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_AllProductsResolved_ShouldCallSetSnapshotsWithAllItems()
        {
            // Arrange
            var item1 = CreateOrderItem(productId: 10);
            var item2 = CreateOrderItem(productId: 20);
            var snapshot1 = new OrderProductSnapshot("Product A", "a.jpg", 1);
            var snapshot2 = new OrderProductSnapshot("Product B", null, null);

            SetupOrderItems(1, item1, item2);
            SetupProductSnapshots(new Dictionary<int, OrderProductSnapshot> { [10] = snapshot1, [20] = snapshot2 });

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), CancellationToken.None);

            // Assert
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(
                It.Is<IReadOnlyList<(int, OrderProductSnapshot)>>(l => l.Count == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SomeProductsNotFound_ShouldCallSetSnapshotsWithResolvedOnly()
        {
            // Arrange
            var item1 = CreateOrderItem(productId: 10);
            var item2 = CreateOrderItem(productId: 99);
            var snapshot1 = new OrderProductSnapshot("Product A", "a.jpg", 1);

            SetupOrderItems(1, item1, item2);
            SetupProductSnapshots(new Dictionary<int, OrderProductSnapshot> { [10] = snapshot1 });

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), CancellationToken.None);

            // Assert
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(
                It.Is<IReadOnlyList<(int, OrderProductSnapshot)>>(l => l.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_AllProductsNotFound_ShouldNotCallSetSnapshotsAsync()
        {
            // Arrange
            var item1 = CreateOrderItem(productId: 99);

            SetupOrderItems(1, item1);
            SetupProductSnapshots(new Dictionary<int, OrderProductSnapshot>());

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), CancellationToken.None);

            // Assert
            _orderItemRepo.Verify(r => r.SetSnapshotsAsync(It.IsAny<IReadOnlyList<(int, OrderProductSnapshot)>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
