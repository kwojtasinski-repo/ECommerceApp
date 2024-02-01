﻿using System.Collections.Generic;
using System.Linq;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.ViewModels.Coupon;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{MaintenanceRole}")]
    public class CouponController : BaseController
    {
        private readonly ICouponService _couponService;
        private readonly ICouponTypeService _couponTypeService;
        private readonly ICouponUsedService _couponUsedService;

        public CouponController(ICouponService couponService, ICouponTypeService couponTypeService, ICouponUsedService couponUsedService)
        {
            _couponService = couponService;
            _couponTypeService = couponTypeService;
            _couponUsedService = couponUsedService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = _couponService.GetAllCoupons(20, 1, "");

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
            var model = _couponService.GetAllCoupons(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public IActionResult AddCoupon()
        {
            var couponTypes = _couponTypeService.GetAllCouponsTypes();
            ViewBag.CouponTypes = couponTypes;
            return View(new CouponVm());
        }

        [HttpPost]
        public IActionResult AddCoupon(CouponVm model)
        {
            try
            {
                _couponService.AddCoupon(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult EditCoupon(int id)
        {
            var coupon = _couponService.GetCoupon(id);
            if (coupon is null)
            {
                var errorModel = BuildErrorModel("couponNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new CouponDetailsVm());
            }
            var couponTypes = _couponTypeService.GetAllCouponsTypes().ToList();
            ViewBag.CouponTypes = couponTypes;
            return View(coupon);
        }

        [HttpPost]
        public IActionResult EditCoupon(CouponVm model)
        {
            try
            {
                _couponService.UpdateCoupon(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult ViewCoupon(int id)
        {
            var coupon = _couponService.GetCouponDetail(id);
            if (coupon is null)
            {
                var errorModel = BuildErrorModel("couponNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new CouponDetailsVm());
            }
            return View(coupon);
        }

        [HttpDelete]
        public IActionResult DeleteCoupon(int id)
        {
            try
            {
                _couponService.DeleteCoupon(id);
                return Json("deleted");
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }

        [HttpGet]
        public IActionResult GetByCode(string couponCoude)
        {
            return Ok(_couponService.GetCouponByCode(couponCoude));
        }
    }
}
