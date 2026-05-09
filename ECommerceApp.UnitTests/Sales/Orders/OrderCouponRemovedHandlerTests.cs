using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Sales.Orders.Services;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderCouponRemovedHandlerTests
    {
        private readonly Mock<IOrderService> _orders;

        public OrderCouponRemovedHandlerTests()
        {
            _orders = new Mock<IOrderService>();
        }

        private OrderCouponRemovedHandler CreateHandler() => new(_orders.Object);

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldCallRemoveCouponAsyncWithCorrectOrderId()
        {
            var message = new CouponRemovedFromOrder(OrderId: 42);

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            _orders.Verify(s => s.RemoveCouponAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
