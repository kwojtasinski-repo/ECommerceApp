using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using ECommerceApp.Domain.Sales.Orders;
using AwesomeAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficeOrderServiceTests
    {
        private readonly Mock<IOrderService> _orderService;

        public BackofficeOrderServiceTests()
        {
            _orderService = new Mock<IOrderService>();
        }

        private IBackofficeOrderService CreateSut() => new BackofficeOrderService(_orderService.Object);

        // ── GetOrdersAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task GetOrdersAsync_WithResults_ReturnsMappedVm()
        {
            // Arrange
            var source = new OrderListVm
            {
                Orders = new List<OrderForListVm>
                {
                    new() { Id = 1, Number = "ORD-001", Cost = 99.99m, Status = OrderStatus.PaymentConfirmed, CustomerId = 10 },
                    new() { Id = 2, Number = "ORD-002", Cost = 49.50m, Status = OrderStatus.Placed,           CustomerId = 20 }
                },
                CurrentPage = 1,
                PageSize = 10,
                TotalCount = 2,
                SearchString = "ORD"
            };
            _orderService
                .Setup(s => s.GetAllOrdersAsync(10, 1, "ORD", It.IsAny<CancellationToken>()))
                .ReturnsAsync(source);

            // Act
            var result = await CreateSut().GetOrdersAsync(10, 1, "ORD");

            // Assert
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(2);
            result.SearchString.Should().Be("ORD");
            result.Orders.Should().HaveCount(2);

            result.Orders[0].Id.Should().Be(1);
            result.Orders[0].Number.Should().Be("ORD-001");
            result.Orders[0].Cost.Should().Be(99.99m);
            result.Orders[0].Status.Should().Be("PaymentConfirmed");
            result.Orders[0].IsPaid.Should().BeTrue();

            result.Orders[1].Id.Should().Be(2);
            result.Orders[1].Status.Should().Be("Placed");
            result.Orders[1].IsPaid.Should().BeFalse();
        }

        [Fact]
        public async Task GetOrdersAsync_CustomerNameIsEmptyForAllItems()
        {
            // Arrange
            _orderService
                .Setup(s => s.GetAllOrdersAsync(10, 1, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderListVm
                {
                    Orders = new List<OrderForListVm>
                    {
                        new() { Id = 3, Number = "ORD-003", Status = OrderStatus.Placed }
                    }
                });

            // Act
            var result = await CreateSut().GetOrdersAsync(10, 1, null);

            // Assert — CustomerName not in OrderForListVm; always empty
            result.Orders[0].CustomerName.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOrdersAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _orderService
                .Setup(s => s.GetAllOrdersAsync(10, 1, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderListVm { Orders = new List<OrderForListVm>(), TotalCount = 0 });

            // Act
            var result = await CreateSut().GetOrdersAsync(10, 1, null);

            // Assert
            result.Orders.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        // ── GetOrderDetailAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetOrderDetailAsync_ExistingOrder_ReturnsMappedVm()
        {
            // Arrange
            var detail = new OrderDetailsVm
            {
                Id = 5,
                Number = "ORD-005",
                Cost = 200m,
                Status = OrderStatus.Fulfilled,
                CustomerId = 42
            };
            _orderService
                .Setup(s => s.GetOrderDetailsAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(detail);

            // Act
            var result = await CreateSut().GetOrderDetailAsync(5);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(5);
            result.Number.Should().Be("ORD-005");
            result.Cost.Should().Be(200m);
            result.Status.Should().Be("Fulfilled");
            result.CustomerId.Should().Be(42);
            result.IsPaid.Should().BeTrue();
            result.IsDelivered.Should().BeTrue();
        }

        [Fact]
        public async Task GetOrderDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            _orderService
                .Setup(s => s.GetOrderDetailsAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync((OrderDetailsVm?)null);

            // Act
            var result = await CreateSut().GetOrderDetailAsync(99);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(OrderStatus.PaymentConfirmed,  true,  false)]
        [InlineData(OrderStatus.PartiallyFulfilled, true, false)]
        [InlineData(OrderStatus.Fulfilled,          true,  true)]
        [InlineData(OrderStatus.Refunded,           true,  true)]
        [InlineData(OrderStatus.Placed,             false, false)]
        [InlineData(OrderStatus.Cancelled,          false, false)]
        public async Task GetOrderDetailAsync_StatusFlags_DerivedCorrectly(
            OrderStatus status, bool expectedIsPaid, bool expectedIsDelivered)
        {
            // Arrange
            _orderService
                .Setup(s => s.GetOrderDetailsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderDetailsVm { Id = 1, Number = "X", Status = status });

            // Act
            var result = await CreateSut().GetOrderDetailAsync(1);

            // Assert
            result!.IsPaid.Should().Be(expectedIsPaid);
            result.IsDelivered.Should().Be(expectedIsDelivered);
        }

        // ── GetOrdersByCustomerAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetOrdersByCustomerAsync_WithOrders_AppliesPaging()
        {
            // Arrange — 5 orders total, request page 2 with size 2
            var all = new List<OrderForListVm>
            {
                new() { Id = 1, Number = "O1", Status = OrderStatus.Placed },
                new() { Id = 2, Number = "O2", Status = OrderStatus.Placed },
                new() { Id = 3, Number = "O3", Status = OrderStatus.Fulfilled },
                new() { Id = 4, Number = "O4", Status = OrderStatus.Placed },
                new() { Id = 5, Number = "O5", Status = OrderStatus.Placed }
            };
            _orderService
                .Setup(s => s.GetOrdersByCustomerIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(all);

            // Act
            var result = await CreateSut().GetOrdersByCustomerAsync(10, pageSize: 2, pageNo: 2);

            // Assert
            result.TotalCount.Should().Be(5);
            result.CurrentPage.Should().Be(2);
            result.PageSize.Should().Be(2);
            result.Orders.Should().HaveCount(2);
            result.Orders[0].Id.Should().Be(3);
            result.Orders[1].Id.Should().Be(4);
        }

        [Fact]
        public async Task GetOrdersByCustomerAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _orderService
                .Setup(s => s.GetOrdersByCustomerIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderForListVm>());

            // Act
            var result = await CreateSut().GetOrdersByCustomerAsync(99, pageSize: 10, pageNo: 1);

            // Assert
            result.Orders.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }
    }
}
