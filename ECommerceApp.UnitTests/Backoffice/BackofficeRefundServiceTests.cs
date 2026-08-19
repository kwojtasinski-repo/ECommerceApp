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

        private void SetupRefundList(RefundListVm source)
        {
            _refundService.Setup(s => s.GetRefundsAsync(10, 1, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(source);
        }

        private void SetupRefundDetails(int refundId, RefundDetailsVm details)
        {
            _refundService.Setup(s => s.GetRefundAsync(refundId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(details);
        }

        private void SetupCustomerId(int orderId, int? customerId)
        {
            _orderService.Setup(s => s.GetCustomerIdAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(customerId);
        }

        private void SetupOrderRefunds(int orderId, IReadOnlyList<RefundVm> refunds)
        {
            _refundService.Setup(s => s.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(refunds);
        }

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
            SetupRefundList(source);

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
            SetupRefundList(new RefundListVm { Refunds = new List<RefundVm>(), TotalCount = 0 });

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

            SetupRefundDetails(5, detail);
            SetupCustomerId(42, 99);

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
            SetupRefundDetails(5, new RefundDetailsVm(5, 42, "X", false, "Approved",
                DateTime.UtcNow, null, new List<RefundItemVm>(), "user-1"));
            SetupCustomerId(42, null);

            // Act
            var result = await CreateSut().GetRefundDetailAsync(5, TestContext.Current.CancellationToken);

            // Assert
            result!.CustomerId.Should().Be(0);
        }

        [Fact]
        public async Task GetRefundDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            SetupRefundDetails(99, null);

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
            SetupOrderRefunds(10, new List<RefundVm>
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
            SetupOrderRefunds(99, new List<RefundVm>());

            // Act
            var result = await CreateSut().GetRefundsByOrderAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Refunds.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }
    }
}
