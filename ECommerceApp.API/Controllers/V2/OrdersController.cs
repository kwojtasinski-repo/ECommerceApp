using ECommerceApp.API.Options;
using ECommerceApp.Application.Permissions;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Domain.Sales.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/orders")]
    public class OrdersController : BaseController
    {
        private readonly IOrderService _orders;
        private readonly IPaymentService _payments;
        private readonly IOptions<WebOptions> _webOptions;

        public OrdersController(IOrderService orders, IPaymentService payments, IOptions<WebOptions> webOptions)
        {
            _orders = orders;
            _payments = payments;
            _webOptions = webOptions;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
        {
            var vm = await _orders.GetOrderDetailsAsync(id, ct);
            if (vm is null) return NotFound();

            var userId = GetUserId();
            if (!User.IsInRole(UserPermissions.Roles.Administrator) &&
                !User.IsInRole(UserPermissions.Roles.Manager) &&
                !User.IsInRole(UserPermissions.Roles.Service) &&
                vm.UserId != userId)
                return Forbid();

            string? paymentUrl = null;
            if (vm.Status == OrderStatus.Placed)
            {
                var payment = await _payments.GetPendingByOrderIdAsync(vm.Id, userId, ct);
                if (payment is not null)
                    paymentUrl = $"{_webOptions.Value.BaseUrl}/Sales/Payments/Create/{payment.PaymentId}";
            }

            return Ok(new
            {
                vm.Id,
                vm.Number,
                vm.Cost,
                vm.Ordered,
                Status = vm.Status.ToString(),
                vm.CustomerId,
                vm.CurrencyId,
                vm.CouponUsedId,
                vm.DiscountPercent,
                vm.Customer,
                vm.OrderItems,
                PaymentUrl = paymentUrl
            });
        }
    }
}
