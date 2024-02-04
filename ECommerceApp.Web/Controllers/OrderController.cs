using System.Collections.Generic;
using System.Linq;
using ECommerceApp.Application.ViewModels.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class OrderController : BaseController
    {
        private readonly IOrderService _orderService;
        private readonly IOrderItemService _orderItemService;
        private readonly IItemService _itemService;

        public OrderController(IOrderService orderService, IOrderItemService orderItemService, IItemService itemService)
        {
            _orderService = orderService;
            _orderItemService = orderItemService;
            _itemService = itemService;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _orderService.GetAllOrders(20, 1, "");
            return View(model);
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
            var model = _orderService.GetAllOrders(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public IActionResult AddOrder()
        {
            return View(_orderService.InitOrder());
        }

        [HttpPost]
        public IActionResult AddOrder(OrderVm model)
        {
            try
            {
                var id = _orderService.AddOrder(new AddOrderDto
                {
                    Id = model.Order.Id,
                    CustomerId = model.Order.CustomerId,
                    OrderItems = model.Order.OrderItems?.Select(oi => new OrderItemsIdsDto { Id = oi.Id }).ToList()
                        ?? new List<OrderItemsIdsDto>(),
                });
                return RedirectToAction("AddOrderDetails", new { orderId = id });
            }
            catch(BusinessException exception)
            {
                var errorModel = BuildErrorModel(exception.ErrorCode, exception.Arguments);
                return RedirectToAction("Index", controllerName: "Item", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult AddOrderItemToCart()
        {
            var orderItems = new NewOrderItemVm();
            var items = _itemService.GetItemsAddToCart();
            orderItems.Items = items;
            orderItems.OrderItem.UserId = GetUserId();
            return View(orderItems);
        }

        [HttpPost]
        public IActionResult AddOrderItemToCart(NewOrderItemVm model)
        {
            _orderItemService.AddOrderItem(model.OrderItem);
            return RedirectToAction("Index", controllerName: "Item");
        }

        [HttpGet]
        public IActionResult OrderRealization()
        {
            return View(_orderService.InitOrder());
        }

        [HttpPost]
        public IActionResult OrderRealization(OrderVm model)
        {
            try
            {
                model.Order.Id = _orderService.FulfillOrder(model);
                return RedirectToAction("AddOrderSummary", new { id = model.Order.Id });
            }
            catch(BusinessException exception)
            {
                var errorModel = BuildErrorModel(exception.ErrorCode, exception.Arguments);
                return RedirectToAction("Index", controllerName: "Item", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult AddOrderDetails(int orderId)
        {
            var order = _orderService.GetOrderForRealization(orderId);
            if (order is null)
            {
                var errorModel = BuildErrorModel("contactDetailNotFound", new Dictionary<string, string> { { "id", $"{orderId}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new NewOrderVm());
            }
            return View(order);
        }

        [HttpPost]
        public IActionResult AddOrderDetails(NewOrderVm model)
        {
            try
            {
                if (!model.OrderItems.Any())
                {
                    DeleteOrder(model.Id);
                    return RedirectToAction("Index", controllerName: "Item");
                }

                if (_orderService.UpdateOrder(new UpdateOrderDto
                {
                    Id = model.Id,
                    CouponUsedId = model.CouponUsedId,
                    CustomerId = model.CustomerId,
                    IsDelivered = model.IsDelivered,
                    Ordered = model.Ordered,
                    OrderNumber = model.Number,
                    PromoCode = model.PromoCode,
                    OrderItems = model.OrderItems.Select(oi =>
                        new AddOrderItemDto { Id = oi.Id, ItemId = oi.ItemId, ItemOrderQuantity = oi.ItemOrderQuantity }
                   ).ToList(),
                }) is null)
                {
                    var errorModel = BuildErrorModel("orderNotFound", new Dictionary<string, string> { { "id", $"{model.Id}" } });
                    return RedirectToAction("Index", controllerName: "Item", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
                }
                return RedirectToAction("AddOrderSummary", new { id = model.Id });
            }
            catch (BusinessException exception)
            {
                var errorModel = BuildErrorModel(exception.ErrorCode, exception.Arguments);
                return RedirectToAction("Index", controllerName: "Item", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult AddOrderSummary(int id)
        {
            var order = _orderService.GetOrderSummaryById(id);
            if (order is null)
            {
                var errorModel = BuildErrorModel("orderNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new NewOrderVm());
            }
            return View(order);
        }

        [HttpGet]
        public IActionResult ShowMyCart()
        {
            var userId = GetUserId();
            var orderItems = _orderItemService.GetOrderItemsNotOrderedByUserId(userId, 20, 1);
            return View(orderItems);
        }

        [HttpPost]
        public IActionResult ShowMyCart(int pageSize, int? pageNo)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }
            var userId = GetUserId();
            var orderItems = _orderItemService.GetOrderItemsNotOrderedByUserId(userId, pageSize, pageNo.Value);
            return View(orderItems);
        }

        [HttpGet]
        public IActionResult ShowOrdersByCustomerId(int customerId)
        {
            var orderItems = _orderService.GetAllOrdersByCustomerId(customerId, 20, 1);
            ViewBag.InputParameterId = customerId;
            return View(orderItems);
        }

        [HttpPost]
        public IActionResult ShowOrdersByCustomerId(int customerId, int pageSize, int? pageNo)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }
            ViewBag.InputParameterId = customerId;
            var orderItems = _orderService.GetAllOrdersByCustomerId(customerId, pageSize, pageNo.Value);
            return View(orderItems);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult EditOrder(int id)
        {
            var order = _orderService.BuildVmForEdit(id);
            if (order is null)
            {
                var errorModel = BuildErrorModel("orderNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new NewOrderVm());
            }
            return View(order);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult EditOrder(NewOrderVm model)
        {
            try
            {
                if (_orderService.UpdateOrder(new UpdateOrderDto
                {
                    Id = model.Id,
                    CouponUsedId = model.CouponUsedId,
                    CustomerId = model.CustomerId,
                    IsDelivered = model.IsDelivered,
                    Ordered = model.Ordered,
                    OrderNumber = model.Number,
                    PromoCode = model.PromoCode,
                    Payment = model.PaymentId.HasValue ? new PaymentInfoDto
                    {
                        Id = model.PaymentId,
                        CurrencyId = model.CurrencyId,
                    } : null,
                    OrderItems = model.OrderItems.Select(oi =>
                        new AddOrderItemDto { Id = oi.Id, ItemId = oi.ItemId, ItemOrderQuantity = oi.ItemOrderQuantity }
                    ).ToList(),
                }) is null)
                {
                    var errorModel = BuildErrorModel("orderNotFound", new Dictionary<string, string> { { "id", $"{model.Id}" } });
                    return RedirectToAction("Index", controllerName: "Item", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                var errorModel = BuildErrorModel(exception.ErrorCode, exception.Arguments);
                return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        public IActionResult ViewOrderDetails(int id)
        {
            var order = _orderService.GetOrderDetail(id);
            if (order is null)
            {
                var errorModel = BuildErrorModel("orderNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new OrderDetailsVm());
            }
            return View(order);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeleteOrder(int id)
        {
            try
            {
                return _orderService.DeleteOrder(id)
                    ? Json("deleted")
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }

        public IActionResult ShowMyOrders()
        {
            var userId = GetUserId();
            var orders = _orderService.GetAllOrdersByUserId(userId, 20, 1);
            return View(orders);
        }

        [HttpPost]
        public IActionResult ShowMyOrders(int pageSize, int pageNo)
        {
            var userId = GetUserId();
            var orders = _orderService.GetAllOrdersByUserId(userId, pageSize, pageNo);
            return View(orders);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult ShowOrdersPaid()
        {
            var model = _orderService.GetAllOrdersPaid(20, 1, "");

            return View(model);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult ShowOrdersPaid(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var model = _orderService.GetAllOrdersPaid(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPatch]
        public IActionResult DispatchOrder(int id)
        {
            try
            {
                _orderService.DispatchOrder(id);
                return Ok();
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
