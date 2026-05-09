using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Sales.Orders.Services;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderCouponAppliedHandlerTests
    {
        private readonly Mock<IOrderService> _orders;

        public OrderCouponAppliedHandlerTests()
        {
            _orders = new Mock<IOrderService>();
        }

        private OrderCouponAppliedHandler CreateHandler() => new(_orders.Object);

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldCallAddCouponAsyncWithCorrectParameters()
        {
            var message = new CouponApplied(OrderId: 7, CouponUsedId: 3, DiscountPercent: 15);

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            _orders.Verify(s => s.AddCouponAsync(7, 3, 15, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
