using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Supporting.Verification.Services;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using ECommerceApp.Domain.Supporting.Verification;
using ECommerceApp.Infrastructure.Presale.Checkout.Adapters;
using AwesomeAssertions;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class VerificationCodeClientAdapterTests
    {
        private readonly Mock<IVerificationCodeService> _verificationCodes = new();
        private readonly Mock<IOrderService> _orders = new();

        [Fact]
        public async Task RequestOrderAccessRecoveryAsync_UsesOrderPurposeAndSubject()
        {
            _verificationCodes.Setup(service => service.GenerateAsync(
                    VerificationPurpose.GuestOrderAccess,
                    "42",
                    It.IsAny<System.TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("code");

            var result = await CreateAdapter().RequestOrderAccessRecoveryAsync(42);

            result.Should().Be("code");
            _verificationCodes.Verify(service => service.GenerateAsync(
                VerificationPurpose.GuestOrderAccess,
                "42",
                It.IsAny<System.TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RedeemOrderAccessRecoveryAsync_ResolvesOnlyTheSubjectOrder()
        {
            _verificationCodes.Setup(service => service.TryConsumeAsync(
                    "code",
                    VerificationPurpose.GuestOrderAccess,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("42");
            _orders.Setup(service => service.GetOrderDetailsAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderDetailsVm { Id = 42, CustomerId = 7 });

            var result = await CreateAdapter().RedeemOrderAccessRecoveryAsync("code");

            result.Success.Should().BeTrue();
            result.OrderId.Should().Be(42);
            result.UserProfileId.Should().Be(7);
            _orders.Verify(service => service.GetOrderDetailsAsync(42, It.IsAny<CancellationToken>()), Times.Once);
            _orders.Verify(service => service.GetOrderDetailsAsync(43, It.IsAny<CancellationToken>()), Times.Never);
        }

        private VerificationCodeClientAdapter CreateAdapter()
            => new(_verificationCodes.Object, _orders.Object);
    }
}
