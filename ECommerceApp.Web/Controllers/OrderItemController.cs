using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class OrderItemController : BaseController
    {
        private readonly IOrderItemService _orderItemService;

        public OrderItemController(IOrderItemService orderItemService)
        {
            _orderItemService = orderItemService;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult Index()
        {
            var orderItems = _orderItemService.GetOrderItems(20, 1, "");
            return View(orderItems);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var orderItems = _orderItemService.GetOrderItems(pageSize, pageNo.Value, searchString);
            return View(orderItems);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult ShowOrderItemsByItemId(int itemId)
        {
            var orderItems = _orderItemService.GetAllItemsOrderedByItemId(itemId, 20, 1);
            ViewBag.InputParameterId = itemId;
            return View(orderItems);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult ShowOrderItemsByItemId(int itemId, int pageSize, int? pageNo)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }
            ViewBag.InputParameterId = itemId;
            var orderItems = _orderItemService.GetAllItemsOrderedByItemId(itemId, pageSize, pageNo.Value);
            return View(orderItems);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult ViewOrderItemDetails(int id)
        {
            var orderItem = _orderItemService.GetOrderItemDetails(id);
            if (orderItem is null)
            {
                return NotFound();
            }
            return View(orderItem);
        }

        [HttpGet]
        public IActionResult OrderItemCount()
        {
            var userId = GetUserId();
            var value = _orderItemService.OrderItemCount(userId);
            return Json(new { count = value});
        }

        [HttpPut]
        public IActionResult UpdateOrderItem([FromQuery]int id, [FromBody] OrderItemDto model)
        {
            model.Id = id;
            model.UserId = GetUserId();
            _orderItemService.UpdateOrderItem(model);
            return Json(new { Status = "Updated" });
        }

        [HttpPost]
        public IActionResult AddToCart([FromBody] OrderItemDto model)
        {
            var userId = GetUserId();
            model.UserId = userId;
            var id = _orderItemService.AddOrderItem(model);
            return Json(new { itemId = id });
        }

        public IActionResult DeleteOrderItem(int id)
        {
            try
            {
                _orderItemService.DeleteOrderItem(id);
                return Json(new { });
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
