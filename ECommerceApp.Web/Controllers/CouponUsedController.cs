using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.CouponUsed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{MaintenanceRole}")]
    public class CouponUsedController : BaseController
    {
        private readonly ICouponUsedService _couponUsedService;
        private readonly ICouponTypeService _couponTypeService;
        private readonly IOrderService _orderService;
        private readonly ICouponService _couponService;

        public CouponUsedController(ICouponUsedService couponUsedService, ICouponTypeService couponTypeService, IOrderService orderService,
            ICouponService couponService)
        {
            _couponUsedService = couponUsedService;
            _couponTypeService = couponTypeService;
            _orderService = orderService;
            _couponService = couponService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = _couponUsedService.GetAllCouponsUsed(20, 1, "");

            return View(model);
        }

        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var model = _couponUsedService.GetAllCouponsUsed(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public IActionResult AddCouponUsed()
        {
            var coupons = _couponService.GetAllCouponsNotUsed();
            var orders = _orderService.GetAllOrders();
            ViewBag.Coupons = coupons;
            ViewBag.Orders = orders;
            return View(new CouponUsedVm());
        }

        [HttpPost]
        public IActionResult AddCouponUsed(CouponUsedVm model)
        {
            try
            {
                _couponUsedService.AddCouponUsed(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "Index", MapExceptionAsRouteValues(ex));
            }
        }

        [HttpGet]
        public IActionResult EditCouponUsed(int id)
        {
            var couponUsed = _couponUsedService.GetCouponUsed(id);
            if (couponUsed is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("couponUsedNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new CouponUsedVm());
            }
            var coupons = _couponService.GetAllCouponsNotUsed();
            var orders = _orderService.GetAllOrders();
            ViewBag.Coupons = coupons;
            ViewBag.Orders = orders;
            return View(couponUsed);
        }

        [HttpPost]
        public IActionResult EditCouponUsed(CouponUsedVm model)
        {
            try
            {
                if (!_couponUsedService.UpdateCouponUsed(model))
                {
                    var errorModel = BuildErrorModel(ErrorCode.Create("couponUsedNotFound", ErrorParameter.Create("id", model.Id)));
                    return RedirectToAction(actionName: "Index", errorModel.AsOjectRoute());
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "Index", MapExceptionAsRouteValues(ex));
            }
        }

        [HttpGet]
        public IActionResult ViewCouponUsed(int id)
        {
            var couponUsed = _couponUsedService.GetCouponUsedDetail(id);
            if (couponUsed is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("couponUsedNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new CouponUsedDetailsVm());
            }
            return View(couponUsed);
        }

        public IActionResult DeleteCouponUsed(int id)
        {
            try
            {
                return _couponUsedService.DeleteCouponUsed(id)
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
