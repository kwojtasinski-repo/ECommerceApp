using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize]
    public class RefundController : BaseController
    {
        private readonly IRefundService _refundService;
        private readonly IOrderService _orderService;

        public RefundController(IRefundService refundService, IOrderService orderService)
        {
            _refundService = refundService;
            _orderService = orderService;
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _refundService.GetRefundsAsync(20, 1, string.Empty);
            return View(model);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string? searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var model = await _refundService.GetRefundsAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var refund = await _refundService.GetRefundAsync(id);
            if (refund is null)
                return NotFound();
            return View(refund);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var result = await _refundService.ApproveRefundAsync(id);
            if (result == RefundOperationResult.RefundNotFound)
                return NotFound();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var result = await _refundService.RejectRefundAsync(id);
            if (result == RefundOperationResult.RefundNotFound)
                return NotFound();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> View(int id)
        {
            var refund = await _refundService.GetRefundAsync(id);
            if (refund is null)
                return NotFound();
            if (refund.UserId != GetUserId())
                return Forbid();
            return View(refund);
        }

        [HttpGet]
        public async Task<IActionResult> Request(int orderId)
        {
            var order = await _orderService.GetOrderDetailsAsync(orderId);
            if (order is null)
                return NotFound();
            if (order.UserId != GetUserId())
                return Forbid();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Request(int orderId, string reason, bool onWarranty, int[] productIds, int[] quantities)
        {
            var userId = GetUserId();
            var order = await _orderService.GetOrderDetailsAsync(orderId);
            if (order is null)
                return NotFound();
            if (order.UserId != userId)
                return Forbid();

            var items = productIds
                .Zip(quantities, (pid, qty) => new RequestRefundItemDto(pid, qty))
                .ToList();

            var dto = new RequestRefundDto(orderId, reason, onWarranty, items, userId);
            var result = await _refundService.RequestRefundAsync(dto);

            if (result == RefundRequestResult.OrderNotFound)
                return NotFound();

            return RedirectToAction(nameof(MyRefunds));
        }

        [HttpGet]
        public async Task<IActionResult> MyRefunds()
        {
            var userId = GetUserId();
            var model = await _refundService.GetRefundsAsync(20, 1, userId);
            return View(model);
        }
    }
}
