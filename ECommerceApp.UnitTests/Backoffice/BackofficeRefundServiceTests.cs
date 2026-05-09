using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Application.Sales.Fulfillment.ViewModels;
using ECommerceApp.Application.Sales.Orders.Services;
using AwesomeAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficeRefundServiceTests
    {
        private readonly Mock<IRefundService> _refundService;
        private readonly Mock<IOrderService> _orderService;

        public BackofficeRefundServiceTests()
        {
            _refundService = new Mock<IRefundService>();
            _orderService = new Mock<IOrderService>();
        }

        private IBackofficeRefundService CreateSut()
            => new BackofficeRefundService(_refundService.Object, _orderService.Object);

        // ── GetRefundsAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetRefundsAsync_WithResults_ReturnsMappedVm()
        {
            // Arrange
            var source = new RefundListVm
            {
                Refunds = new List<RefundVm>
                {
                    new(1, 10, "Damaged", true,  "Requested",  DateTime.UtcNow, null,     "user-1"),
                    new(2, 20, "Wrong",   false, "Approved",   DateTime.UtcNow, DateTime.UtcNow, "user-2")
                },
                CurrentPage = 1,
                PageSize = 10,
                TotalCount = 2
            };
            _refundService
                .Setup(s => s.GetRefundsAsync(10, 1, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(source);

            // Act
            var result = await CreateSut().GetRefundsAsync(10, 1, TestContext.Current.CancellationToken);

            // Assert
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(2);
            result.Refunds.Should().HaveCount(2);

            result.Refunds[0].Id.Should().Be(1);
            result.Refunds[0].OrderId.Should().Be(10);
            result.Refunds[0].Reason.Should().Be("Damaged");
            result.Refunds[0].Status.Should().Be("Requested");
            result.Refunds[0].OnWarranty.Should().BeTrue();

            result.Refunds[1].Id.Should().Be(2);
            result.Refunds[1].Status.Should().Be("Approved");
        }

        [Fact]
        public async Task GetRefundsAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _refundService
                .Setup(s => s.GetRefundsAsync(10, 1, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefundListVm { Refunds = new List<RefundVm>(), TotalCount = 0 });

            // Act
            var result = await CreateSut().GetRefundsAsync(10, 1, TestContext.Current.CancellationToken);

            // Assert
            result.Refunds.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        // ── GetRefundDetailAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetRefundDetailAsync_ExistingRefund_ReturnsMappedVmWithCustomerId()
        {
            // Arrange
            var detail = new RefundDetailsVm(5, 42, "Broken", true, "Requested",
                DateTime.UtcNow, null, new List<RefundItemVm>(), "user-1");

            _refundService
                .Setup(s => s.GetRefundAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(detail);
            _orderService
                .Setup(s => s.GetCustomerIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(99);

            // Act
            var result = await CreateSut().GetRefundDetailAsync(5, TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(5);
            result.OrderId.Should().Be(42);
            result.CustomerId.Should().Be(99);
            result.Reason.Should().Be("Broken");
            result.Status.Should().Be("Requested");
            result.OnWarranty.Should().BeTrue();
        }

        [Fact]
        public async Task GetRefundDetailAsync_CustomerIdNullFromOrder_UsesZero()
        {
            // Arrange
            _refundService
                .Setup(s => s.GetRefundAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefundDetailsVm(5, 42, "X", false, "Approved",
                    DateTime.UtcNow, null, new List<RefundItemVm>(), "user-1"));
            _orderService
                .Setup(s => s.GetCustomerIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null);

            // Act
            var result = await CreateSut().GetRefundDetailAsync(5, TestContext.Current.CancellationToken);

            // Assert
            result!.CustomerId.Should().Be(0);
        }

        [Fact]
        public async Task GetRefundDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            _refundService
                .Setup(s => s.GetRefundAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync((RefundDetailsVm)null);

            // Act
            var result = await CreateSut().GetRefundDetailAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }

        // ── GetRefundsByOrderAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetRefundsByOrderAsync_WithRefunds_ReturnsMappedList()
        {
            // Arrange
            _refundService
                .Setup(s => s.GetByOrderIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RefundVm>
                {
                    new(1, 10, "Damaged", true, "Requested", DateTime.UtcNow, null, "user-1"),
                    new(2, 10, "Wrong",   false, "Approved", DateTime.UtcNow, null, "user-2")
                });

            // Act
            var result = await CreateSut().GetRefundsByOrderAsync(10, TestContext.Current.CancellationToken);

            // Assert
            result.Refunds.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
            result.CurrentPage.Should().Be(1);
            result.Refunds[0].OrderId.Should().Be(10);
            result.Refunds[1].OrderId.Should().Be(10);
        }

        [Fact]
        public async Task GetRefundsByOrderAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _refundService
                .Setup(s => s.GetByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RefundVm>());

            // Act
            var result = await CreateSut().GetRefundsByOrderAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Refunds.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }
    }
}
