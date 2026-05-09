using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Application.Sales.Coupons.ViewModels;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficeCouponServiceTests
    {
        private readonly Mock<ICouponService> _couponService;

        public BackofficeCouponServiceTests()
        {
            _couponService = new Mock<ICouponService>();
        }

        private IBackofficeCouponService CreateSut() => new BackofficeCouponService(_couponService.Object);

        // ── GetCouponsAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetCouponsAsync_WithResults_ReturnsMappedVm()
        {
            // Arrange
            var source = new CouponListVm
            {
                Coupons = new List<CouponForListVm>
                {
                    new() { Id = 1, Code = "SAVE10", Description = "10% off", Status = "Available" },
                    new() { Id = 2, Code = "HALF",   Description = "50% off", Status = "Used"      }
                },
                CurrentPage = 1,
                PageSize = 10,
                TotalCount = 2,
                SearchString = "off"
            };
            _couponService
                .Setup(s => s.GetCouponsAsync(10, 1, "off", It.IsAny<CancellationToken>()))
                .ReturnsAsync(source);

            // Act
            var result = await CreateSut().GetCouponsAsync(10, 1, "off", TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(2);
            result.SearchString.Should().Be("off");
            result.Coupons.Should().HaveCount(2);

            result.Coupons[0].Id.Should().Be(1);
            result.Coupons[0].Code.Should().Be("SAVE10");
            result.Coupons[0].Description.Should().Be("10% off");
            result.Coupons[0].Status.Should().Be("Available");

            result.Coupons[1].Id.Should().Be(2);
            result.Coupons[1].Code.Should().Be("HALF");
            result.Coupons[1].Status.Should().Be("Used");
        }

        [Fact]
        public async Task GetCouponsAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _couponService
                .Setup(s => s.GetCouponsAsync(10, 1, string.Empty, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CouponListVm { Coupons = new List<CouponForListVm>(), TotalCount = 0 });

            // Act
            var result = await CreateSut().GetCouponsAsync(10, 1, null, TestContext.Current.CancellationToken);

            // Assert
            result.Coupons.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task GetCouponsAsync_NullSearchString_PassesEmptyStringToService()
        {
            // Arrange
            _couponService
                .Setup(s => s.GetCouponsAsync(5, 2, string.Empty, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CouponListVm { Coupons = new List<CouponForListVm>() });

            // Act
            await CreateSut().GetCouponsAsync(5, 2, null, TestContext.Current.CancellationToken);

            // Assert
            _couponService.Verify(
                s => s.GetCouponsAsync(5, 2, string.Empty, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ── GetCouponDetailAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetCouponDetailAsync_ExistingCoupon_ReturnsMappedVm()
        {
            // Arrange
            var source = new CouponDetailVm
            {
                Id = 7,
                Code = "VIP20",
                Description = "VIP discount",
                Status = "Available",
                RulesJson = null
            };
            _couponService
                .Setup(s => s.GetCouponAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(source);

            // Act
            var result = await CreateSut().GetCouponDetailAsync(7, TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(7);
            result.Code.Should().Be("VIP20");
            result.Description.Should().Be("VIP discount");
            result.Status.Should().Be("Available");
            result.MaxUsages.Should().BeNull();
        }

        [Fact]
        public async Task GetCouponDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            _couponService
                .Setup(s => s.GetCouponAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CouponDetailVm)null);

            // Act
            var result = await CreateSut().GetCouponDetailAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }
    }
}
