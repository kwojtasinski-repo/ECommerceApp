using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using AwesomeAssertions;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class OrderAccessServiceTests
    {
        private readonly Mock<IOrderAccessTokenRepository> _repository = new();

        [Fact]
        public async Task HasAccessAsync_MatchesOnlyTheOrderBoundToTheToken()
        {
            _repository.Setup(repository => repository.GetByTokenAsync("oat_a", It.IsAny<CancellationToken>()))
                .ReturnsAsync(OrderAccessToken.Create(42, 7, "oat_a"));
            var service = new OrderAccessService(_repository.Object);

            (await service.HasAccessAsync(42, "oat_a")).Should().BeTrue();
            (await service.HasAccessAsync(43, "oat_a")).Should().BeFalse();
        }

        [Fact]
        public async Task GetScopeAsync_InvalidToken_ReturnsNull()
        {
            var service = new OrderAccessService(_repository.Object);

            var result = await service.GetScopeAsync("garbage");

            result.Should().BeNull();
        }
    }
}
