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
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("orderItemNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new OrderItemDto());
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
            try
            {
                model.Id = id;
                model.UserId = GetUserId();
                return _orderItemService.UpdateOrderItem(model)
                    ? Json(new { Status = "Updated" })
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }

        [HttpPost]
        public IActionResult AddToCart([FromBody] OrderItemDto model)
        {
            try
            {
                var userId = GetUserId();
                model.UserId = userId;
                var id = _orderItemService.AddOrderItem(model);
                return Json(new { itemId = id });
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }

        public IActionResult DeleteOrderItem(int id)
        {
            try
            {
                return _orderItemService.DeleteOrderItem(id) 
                    ? Json("deleted")
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }
    }
}
