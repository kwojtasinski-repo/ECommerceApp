using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Coupon;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = "Administrator, Admin, Manager, Service")]
    public class CouponController : Controller
    {
        private readonly ICouponService _couponService;

        public CouponController(ICouponService couponService)
        {
            _couponService = couponService;
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

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var model = _couponService.GetAllCoupons(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [HttpGet]
        public IActionResult ShowCouponTypes()
        {
            var couponTypes = _couponService.GetAllCouponsTypes(20, 1, "");
            return View(couponTypes);
        }

        [HttpPost]
        public IActionResult ShowCouponTypes(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var couponTypes = _couponService.GetAllCouponsTypes(pageSize, pageNo.Value, searchString);
            return View(couponTypes);
        }

        [HttpGet]
        public IActionResult ShowCouponsUsed()
        {
            var couponsUsed = _couponService.GetAllCouponsUsed(20, 1, "");
            return View(couponsUsed);
        }

        [HttpPost]
        public IActionResult ShowCouponsUsed(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var couponTypes = _couponService.GetAllCouponsUsed(pageSize, pageNo.Value, searchString);
            return View(couponTypes);
        }

        [HttpGet]
        public IActionResult AddCoupon()
        {
            var couponTypes = _couponService.GetAllCouponsTypes().ToList();
            ViewBag.CouponTypes = couponTypes;
            return View(new CouponVm());
        }

        [HttpPost]
        public IActionResult AddCoupon(CouponVm model)
        {
            var id = _couponService.AddCoupon(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult AddCouponType()
        {
            return View(new NewCouponTypeVm());
        }

        [HttpPost]
        public IActionResult AddCouponType(NewCouponTypeVm model)
        {
            var id = _couponService.AddCouponType(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult AddCouponUsed()
        {
            var couponTypes = _couponService.GetAllCouponsTypes().ToList();
            var orders = _couponService.GetAllOrders().ToList();
            ViewBag.CouponTypes = couponTypes;
            ViewBag.Orders = orders;
            return View(new NewCouponUsedVm());
        }

        [HttpPost]
        public IActionResult AddCouponUsed(NewCouponUsedVm model)
        {
            var id = _couponService.AddCouponUsed(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditCoupon(int id)
        {
            var coupon = _couponService.GetCouponForEdit(id);
            var couponTypes = _couponService.GetAllCouponsTypes().ToList();
            var couponsUsed = _couponService.GetAllCouponsUsed().ToList();
            ViewBag.CouponTypes = couponTypes;
            ViewBag.CouponsUsed = couponsUsed;
            return View(coupon);
        }

        [HttpPost]
        public IActionResult EditCoupon(CouponVm model)
        {
            _couponService.UpdateCoupon(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditCouponType(int id)
        {
            var couponType = _couponService.GetCouponTypeForEdit(id);
            return View(couponType);
        }

        [HttpPost]
        public IActionResult EditCouponType(NewCouponTypeVm model)
        {
            _couponService.UpdateCouponType(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditCouponUsed(int id)
        {
            var couponUsed = _couponService.GetCouponUsedForEdit(id);
            var couponTypes = _couponService.GetAllCouponsTypes().ToList();
            var couponsUsed = _couponService.GetAllCouponsUsed().ToList();
            ViewBag.CouponTypes = couponTypes;
            ViewBag.CouponsUsed = couponsUsed;
            return View(couponUsed);
        }

        [HttpPost]
        public IActionResult EditCouponUsed(NewCouponUsedVm model)
        {
            _couponService.UpdateCouponUsed(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult ViewCoupon(int id)
        {
            var coupon = _couponService.GetCouponDetail(id);
            return View(coupon);
        }

        [HttpGet]
        public IActionResult ViewCouponType(int id)
        {
            var couponType = _couponService.GetCouponTypeDetail(id);
            return View(couponType);
        }

        [HttpGet]
        public IActionResult ViewCouponUsed(int id)
        {
            var couponUsed = _couponService.GetCouponUsedDetail(id);
            return View(couponUsed);
        }

        public IActionResult DeleteCoupon(int id)
        {
            _couponService.DeleteCoupon(id);
            return RedirectToAction("Index");
        }

        public IActionResult DeleteCouponType(int id)
        {
            _couponService.DeleteCouponType(id);
            return RedirectToAction("Index");
        }

        public IActionResult DeleteCouponUsed(int id)
        {
            _couponService.DeleteCouponUsed(id);
            return RedirectToAction("Index");
        }
    }
}
