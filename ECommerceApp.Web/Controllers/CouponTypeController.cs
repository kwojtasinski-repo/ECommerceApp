using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.ViewModels.CouponType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

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
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult EditCouponType(int id)
        {
            var couponType = _couponService.GetCouponType(id);
            if (couponType is null)
            {
                var errorModel = BuildErrorModel("couponTypeNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new CouponTypeVm());
            }
            return View(couponType);
        }

        [HttpPost]
        public IActionResult EditCouponType(CouponTypeVm model)
        {
            try
            {
                _couponService.UpdateCouponType(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult ViewCouponType(int id)
        {
            var couponType = _couponService.GetCouponTypeDetail(id);
            if (couponType is null)
            {
                var errorModel = BuildErrorModel("couponTypeNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new CouponTypeDetailsVm());
            }
            return View(couponType);
        }

        public IActionResult DeleteCouponType(int id)
        {
            try
            {
                _couponService.DeleteCouponType(id);
                return Json("deleted");
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
