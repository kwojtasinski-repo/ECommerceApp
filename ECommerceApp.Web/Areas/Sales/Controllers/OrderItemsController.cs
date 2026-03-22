using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize]
    public class OrderItemsController : BaseController
    {
        private readonly IOrderItemService _orderItemService;

        public OrderItemsController(IOrderItemService orderItemService)
        {
            _orderItemService = orderItemService;
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _orderItemService.GetAllPagedAsync(20, 1, string.Empty);
            return View(model);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string? searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var model = await _orderItemService.GetAllPagedAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> ByItem(int id)
        {
            var model = await _orderItemService.GetAllPagedAsync(20, 1, id.ToString());
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var item = await _orderItemService.GetByIdAsync(id);
            if (item is null)
                return NotFound();
            return View(item);
        }
    }
}
