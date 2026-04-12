using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Sales.Payments.ViewModels;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficePaymentServiceTests
    {
        private readonly Mock<IPaymentService> _paymentService;
        private readonly Mock<IOrderService> _orderService;

        public BackofficePaymentServiceTests()
        {
            _paymentService = new Mock<IPaymentService>();
            _orderService = new Mock<IOrderService>();
        }

        private IBackofficePaymentService CreateSut()
            => new BackofficePaymentService(_paymentService.Object, _orderService.Object);

        private static PaymentListVm MakeList(params PaymentVm[] items)
            => new(items, 1, items.Length == 0 ? 10 : items.Length, items.Length);

        // ── GetPaymentsAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetPaymentsAsync_WithResults_ReturnsMappedVm()
        {
            // Arrange
            var list = new PaymentListVm(
                new List<PaymentVm>
                {
                    new(1, 10, 99.99m, 1, "Pending",   DateTime.UtcNow.AddDays(1), null),
                    new(2, 20, 49.50m, 1, "Confirmed", DateTime.UtcNow.AddDays(2), DateTime.UtcNow)
                },
                CurrentPage: 1, PageSize: 10, TotalCount: 2);
            _paymentService
                .Setup(s => s.GetAllAsync(10, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            // Act
            var result = await CreateSut().GetPaymentsAsync(10, 1);

            // Assert
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(2);
            result.Payments.Should().HaveCount(2);

            result.Payments[0].Id.Should().Be(1);
            result.Payments[0].OrderId.Should().Be(10);
            result.Payments[0].Cost.Should().Be(99.99m);
            result.Payments[0].State.Should().Be("Pending");
        }

        [Fact]
        public async Task GetPaymentsAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _paymentService
                .Setup(s => s.GetAllAsync(10, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentListVm(new List<PaymentVm>(), 1, 10, 0));

            // Act
            var result = await CreateSut().GetPaymentsAsync(10, 1);

            // Assert
            result.Payments.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        // ── GetPaymentDetailAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetPaymentDetailAsync_ExistingPayment_ReturnsMappedVmWithCustomerId()
        {
            // Arrange
            var paymentGuid = Guid.NewGuid();
            var detail = new PaymentDetailsVm(5, paymentGuid, 42, 200m, 1, "Confirmed",
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow, "TXN-001", "user-1");
            _paymentService
                .Setup(s => s.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(detail);
            _orderService
                .Setup(s => s.GetCustomerIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(77);

            // Act
            var result = await CreateSut().GetPaymentDetailAsync(5);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(5);
            result.OrderId.Should().Be(42);
            result.CustomerId.Should().Be(77);
            result.Cost.Should().Be(200m);
            result.State.Should().Be("Confirmed");
            result.Number.Should().Be(paymentGuid.ToString());
        }

        [Fact]
        public async Task GetPaymentDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            _paymentService
                .Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentDetailsVm?)null);

            // Act
            var result = await CreateSut().GetPaymentDetailAsync(99);

            // Assert
            result.Should().BeNull();
        }

        // ── GetUnpaidOrderPaymentsAsync ───────────────────────────────────────

        [Fact]
        public async Task GetUnpaidOrderPaymentsAsync_WithResults_ReturnsMappedVm()
        {
            // Arrange
            var list = new PaymentListVm(
                new List<PaymentVm>
                {
                    new(3, 30, 150m, 1, "Pending", DateTime.UtcNow.AddDays(1), null)
                },
                CurrentPage: 1, PageSize: 10, TotalCount: 1);
            _paymentService
                .Setup(s => s.GetAllUnpaidAsync(10, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            // Act
            var result = await CreateSut().GetUnpaidOrderPaymentsAsync(10, 1);

            // Assert
            result.TotalCount.Should().Be(1);
            result.Payments.Should().HaveCount(1);
            result.Payments[0].State.Should().Be("Pending");
        }
    }
}
