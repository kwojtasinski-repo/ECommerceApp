using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.ViewModels.CouponType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{MaintenanceRole}")]
    public class CouponTypeController : BaseController
    {
        private readonly ICouponTypeService _couponService;

        public CouponTypeController(ICouponTypeService couponService)
        {
            _couponService = couponService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = _couponService.GetAllCouponsTypes(20, 1, "");

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
            var model = _couponService.GetAllCouponsTypes(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public IActionResult AddCouponType()
        {
            return View(new CouponTypeVm());
        }

        [HttpPost]
        public IActionResult AddCouponType(CouponTypeVm model)
        {
            try
            {
                _couponService.AddCouponType(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "Index", MapExceptionAsRouteValues(ex));
            }
        }

        [HttpGet]
        public IActionResult EditCouponType(int id)
        {
            var couponType = _couponService.GetCouponType(id);
            if (couponType is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("couponTypeNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new CouponTypeVm());
            }
            return View(couponType);
        }

        [HttpPost]
        public IActionResult EditCouponType(CouponTypeVm model)
        {
            try
            {
                if (!_couponService.UpdateCouponType(model))
                {
                    return RedirectToAction(actionName: "Index", BuildErrorModel(ErrorCode.Create("couponTypeNotFound", ErrorParameter.Create("id", model.Id))).AsOjectRoute());
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "Index", MapExceptionAsRouteValues(ex));
            }
        }

        [HttpGet]
        public IActionResult ViewCouponType(int id)
        {
            var couponType = _couponService.GetCouponTypeDetail(id);
            if (couponType is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("couponTypeNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new CouponTypeDetailsVm());
            }
            return View(couponType);
        }

        public IActionResult DeleteCouponType(int id)
        {
            try
            {
                return _couponService.DeleteCouponType(id)
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
