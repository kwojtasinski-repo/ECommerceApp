using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Infrastructure.Sales.Orders.Adapters;
using AwesomeAssertions;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderAccessClientAdapterTests
    {
        [Fact]
        public async Task HasAccessAsync_DelegatesToPresaleOrderAccessService()
        {
            var service = new Mock<IOrderAccessService>();
            service.Setup(value => value.HasAccessAsync(42, "oat_a", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(true);

            var result = await new OrderAccessClientAdapter(service.Object)
                .HasAccessAsync(42, "oat_a");

            result.Should().BeTrue();
            service.Verify(value => value.HasAccessAsync(42, "oat_a", It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }
    }
}
