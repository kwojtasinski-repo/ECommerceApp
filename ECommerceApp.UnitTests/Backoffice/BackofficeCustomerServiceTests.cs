using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.AccountProfile.ViewModels;
using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using ECommerceApp.Domain.Sales.Orders;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficeCustomerServiceTests
    {
        private readonly Mock<IUserProfileService> _profileService;
        private readonly Mock<IOrderService> _orderService;

        public BackofficeCustomerServiceTests()
        {
            _profileService = new Mock<IUserProfileService>();
            _orderService = new Mock<IOrderService>();
        }

        private IBackofficeCustomerService CreateSut()
            => new BackofficeCustomerService(_profileService.Object, _orderService.Object);

        // ── GetCustomersAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetCustomersAsync_WithResults_ReturnsMappedVm()
        {
            // Arrange
            var source = new UserProfileListVm
            {
                Profiles = new List<UserProfileForListVm>
                {
                    new() { Id = 1, UserId = "user-1", FirstName = "Jan",   LastName = "Kowalski", IsCompany = false },
                    new() { Id = 2, UserId = "user-2", FirstName = "Firma", LastName = "Sp.z.o.o", IsCompany = true  }
                },
                CurrentPage = 1,
                PageSize = 10,
                Count = 2,
                SearchString = "Jan"
            };
            _profileService
                .Setup(s => s.GetAllAsync(10, 1, "Jan"))
                .ReturnsAsync(source);

            // Act
            var result = await CreateSut().GetCustomersAsync(10, 1, "Jan");

            // Assert
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(2);
            result.SearchString.Should().Be("Jan");
            result.Customers.Should().HaveCount(2);

            result.Customers[0].Id.Should().Be(1);
            result.Customers[0].FullName.Should().Be("Jan Kowalski");
            result.Customers[0].UserId.Should().Be("user-1");
            result.Customers[0].IsCompany.Should().BeFalse();

            result.Customers[1].IsCompany.Should().BeTrue();
        }

        [Fact]
        public async Task GetCustomersAsync_NullSearch_DelegatesToEmptyString()
        {
            // Arrange
            _profileService
                .Setup(s => s.GetAllAsync(10, 1, string.Empty))
                .ReturnsAsync(new UserProfileListVm { Profiles = new List<UserProfileForListVm>() });

            // Act
            var result = await CreateSut().GetCustomersAsync(10, 1, null);

            // Assert
            result.Customers.Should().BeEmpty();
            _profileService.Verify(s => s.GetAllAsync(10, 1, string.Empty), Times.Once);
        }

        // ── GetCustomerDetailAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetCustomerDetailAsync_ExistingCustomer_ReturnsMappedVm()
        {
            // Arrange
            _profileService
                .Setup(s => s.GetDetailsAsync(5))
                .ReturnsAsync(new UserProfileDetailsVm
                {
                    Id = 5,
                    UserId = "user-5",
                    FirstName = "Anna",
                    LastName = "Nowak",
                    IsCompany = false
                });

            // Act
            var result = await CreateSut().GetCustomerDetailAsync(5);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(5);
            result.FirstName.Should().Be("Anna");
            result.LastName.Should().Be("Nowak");
            result.UserId.Should().Be("user-5");
            result.IsCompany.Should().BeFalse();
        }

        [Fact]
        public async Task GetCustomerDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            _profileService
                .Setup(s => s.GetDetailsAsync(99))
                .ReturnsAsync((UserProfileDetailsVm?)null);

            // Act
            var result = await CreateSut().GetCustomerDetailAsync(99);

            // Assert
            result.Should().BeNull();
        }

        // ── GetOrdersByCustomerAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetOrdersByCustomerAsync_WithOrders_AppliesPaging()
        {
            // Arrange — 5 orders, request page 2 with size 2
            var all = new List<OrderForListVm>
            {
                new() { Id = 1, Number = "O1", Cost = 10m, Status = OrderStatus.Placed },
                new() { Id = 2, Number = "O2", Cost = 20m, Status = OrderStatus.Placed },
                new() { Id = 3, Number = "O3", Cost = 30m, Status = OrderStatus.PaymentConfirmed },
                new() { Id = 4, Number = "O4", Cost = 40m, Status = OrderStatus.Fulfilled },
                new() { Id = 5, Number = "O5", Cost = 50m, Status = OrderStatus.Placed }
            };
            _orderService
                .Setup(s => s.GetOrdersByCustomerIdAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(all);

            // Act
            var result = await CreateSut().GetOrdersByCustomerAsync(7, pageSize: 2, pageNo: 2);

            // Assert
            result.TotalCount.Should().Be(5);
            result.CurrentPage.Should().Be(2);
            result.PageSize.Should().Be(2);
            result.Orders.Should().HaveCount(2);
            result.Orders[0].Id.Should().Be(3);
            result.Orders[0].IsPaid.Should().BeTrue();
            result.Orders[1].Id.Should().Be(4);
            result.Orders[1].IsPaid.Should().BeTrue();
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
