using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Web.Areas.Presale.Authorization;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize(Policy = AppAuthorizationPolicies.CustomerOrGuestPolicy)]
    public class OrdersController : BaseController
    {
        private readonly IOrderService _orderService;
        private readonly IOrderAccessAuthorizer _orderAccessAuthorizer;

        public OrdersController(IOrderService orderService, IOrderAccessAuthorizer orderAccessAuthorizer)
        {
            _orderService = orderService;
            _orderAccessAuthorizer = orderAccessAuthorizer;
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _orderService.GetAllOrdersAsync(20, 1, string.Empty);
            return View(model);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var model = await _orderService.GetAllOrdersAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MyOrders()
        {
            var userId = GetUserId();
            var orders = await _orderService.GetOrdersByUserIdAsync(userId);
            orders = GuestAccessScope.ScopeToCurrentOrder(User, orders, o => o.Id);
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _orderService.GetOrderDetailsAsync(id);
            if (order is null)
                return NotFound();
            var authorized = await _orderAccessAuthorizer.AuthorizeAsync(
                User,
                new OrderAccessResource(order.Id, order.UserId),
                HttpContext.RequestAborted);
            if (!authorized)
                return OrderAccessDenial.Result(this, User, order.Id);
            return View(order);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _orderService.GetOrderDetailsAsync(id);
            if (order is null)
                return NotFound();
            return View(order);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateOrderDto dto)
        {
            await _orderService.UpdateOrderAsync(dto);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> ByCustomer(int id)
        {
            var orders = await _orderService.GetOrdersByCustomerIdAsync(id);
            return View(orders);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> PaidOrders()
        {
            var model = await _orderService.GetAllPaidOrdersAsync(20, 1, string.Empty);
            return View(model);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        public async Task<IActionResult> PaidOrders(int pageSize, int? pageNo, string searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var model = await _orderService.GetAllPaidOrdersAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispatch(int id)
        {
            await _orderService.MarkAsDeliveredAsync(id);
            return RedirectToAction(nameof(PaidOrders));
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> Fulfillment(int id)
        {
            var order = await _orderService.GetOrderDetailsAsync(id);
            if (order is null)
                return NotFound();
            return View(order);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            await _orderService.CancelOrderAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
