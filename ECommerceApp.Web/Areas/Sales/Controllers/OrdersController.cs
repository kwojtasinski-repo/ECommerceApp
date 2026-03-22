using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize]
    public class OrdersController : BaseController
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
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
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string? searchString)
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
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _orderService.GetOrderDetailsAsync(id);
            if (order is null)
                return NotFound();
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
        public async Task<IActionResult> PaidOrders(int pageSize, int? pageNo, string? searchString)
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
