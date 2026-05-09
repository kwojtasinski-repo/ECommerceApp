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
    [Authorize(Roles = MaintenanceRole)]
    public class ShipmentController : BaseController
    {
        private readonly IShipmentService _shipmentService;
        private readonly IOrderService _orderService;

        public ShipmentController(IShipmentService shipmentService, IOrderService orderService)
        {
            _shipmentService = shipmentService;
            _orderService = orderService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _shipmentService.GetAllShipmentsAsync(20, 1, string.Empty);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var model = await _shipmentService.GetAllShipmentsAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var shipment = await _shipmentService.GetShipmentAsync(id);
            if (shipment is null)
                return NotFound();
            return View(shipment);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int orderId)
        {
            var order = await _orderService.GetOrderDetailsAsync(orderId);
            if (order is null)
                return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int orderId, int[] productIds, int[] quantities)
        {
            if (productIds is null || quantities is null || productIds.Length == 0 || productIds.Length != quantities.Length)
            {
                ModelState.AddModelError(string.Empty, "Wymagana co najmniej jedna pozycja.");
                var order = await _orderService.GetOrderDetailsAsync(orderId);
                return View(order);
            }

            var lines = productIds.Zip(quantities, (pid, qty) => new CreateShipmentLineDto(pid, qty)).ToList();
            var dto = new CreateShipmentDto(orderId, lines);
            var result = await _shipmentService.CreateShipmentAsync(dto);

            if (result == ShipmentOperationResult.OrderNotFound)
                return NotFound();

            return RedirectToAction(nameof(OrderShipments), new { orderId });
        }

        [HttpGet]
        public async Task<IActionResult> OrderShipments(int orderId)
        {
            var shipments = await _shipmentService.GetShipmentsByOrderIdAsync(orderId);
            ViewBag.OrderId = orderId;
            return View(shipments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispatch(int id, string trackingNumber)
        {
            var result = await _shipmentService.MarkAsInTransitAsync(id, trackingNumber);
            if (result == ShipmentOperationResult.NotFound)
                return NotFound();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deliver(int id)
        {
            var result = await _shipmentService.MarkAsDeliveredAsync(id);
            if (result == ShipmentOperationResult.NotFound)
                return NotFound();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fail(int id)
        {
            var result = await _shipmentService.MarkAsFailedAsync(id);
            if (result == ShipmentOperationResult.NotFound)
                return NotFound();
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
