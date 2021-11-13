using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.CouponType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = "Administrator, Admin, Manager, Service")]
    public class CouponTypeController : Controller
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

            if (searchString is null)
            {
                searchString = String.Empty;
            }

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
            var id = _couponService.AddCouponType(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditCouponType(int id)
        {
            var couponType = _couponService.GetCouponType(id);
            return View(couponType);
        }

        [HttpPost]
        public IActionResult EditCouponType(CouponTypeVm model)
        {
            _couponService.UpdateCouponType(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult ViewCouponType(int id)
        {
            var couponType = _couponService.GetCouponTypeDetail(id);
            return View(couponType);
        }

        public IActionResult DeleteCouponType(int id)
        {
            _couponService.DeleteCouponType(id);
            return RedirectToAction("Index");
        }
    }
}
