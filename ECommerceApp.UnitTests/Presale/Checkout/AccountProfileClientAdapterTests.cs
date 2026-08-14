using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Infrastructure.Presale.Checkout.Adapters;
using AwesomeAssertions;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class AccountProfileClientAdapterTests
    {
        [Fact]
        public async Task EnsureGuestCustomerAsync_MapsFieldsAndDelegatesCorrectly()
        {
            var service = new Mock<IUserProfileService>();
            service.Setup(s => s.GetOrCreateForGuestAsync(
                    "gst_1", "Jan", "Kowalski", true, "123", "Acme", "jan@test.com", "123456789", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(12);
            var adapter = new AccountProfileClientAdapter(service.Object);
            var customer = new CheckoutCustomer("Jan", "Kowalski", "jan@test.com", "123456789", true, "Acme", "123", "Street", "1", null, "00-001", "Warsaw", "PL");

            var result = await adapter.EnsureGuestCustomerAsync("gst_1", customer);

            result.Should().Be(12);
            service.Verify(s => s.GetOrCreateForGuestAsync(
                "gst_1", "Jan", "Kowalski", true, "123", "Acme", "jan@test.com", "123456789", It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }
    }
}