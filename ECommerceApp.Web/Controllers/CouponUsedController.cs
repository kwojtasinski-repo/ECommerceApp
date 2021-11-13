using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.CouponUsed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = "Administrator, Admin, Manager, Service")]
    public class CouponUsedController : Controller
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

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var model = _couponUsedService.GetAllCouponsUsed(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [HttpGet]
        public IActionResult AddCouponUsed()
        {
            var coupons = _couponService.GetAllCoupons(c => !c.CouponUsedId.HasValue).ToList();
            var orders = _orderService.GetAllOrders();
            ViewBag.Coupons = coupons;
            ViewBag.Orders = orders;
            return View(new CouponUsedVm());
        }

        [HttpPost]
        public IActionResult AddCouponUsed(CouponUsedVm model)
        {
            var id = _couponUsedService.AddCouponUsed(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditCouponUsed(int id)
        {
            var couponUsed = _couponUsedService.GetCouponUsed(id);
            var coupons = _couponService.GetAllCoupons(c => !c.CouponUsedId.HasValue).ToList();
            var orders = _orderService.GetAllOrders();
            ViewBag.Coupons = coupons;
            ViewBag.Orders = orders;
            return View(couponUsed);
        }

        [HttpPost]
        public IActionResult EditCouponUsed(CouponUsedVm model)
        {
            _couponUsedService.UpdateCouponUsed(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult ViewCouponUsed(int id)
        {
            var couponUsed = _couponUsedService.GetCouponUsedDetail(id);
            return View(couponUsed);
        }

        public IActionResult DeleteCouponUsed(int id)
        {
            _couponUsedService.DeleteCouponUsed(id);
            return RedirectToAction("Index");
        }
    }
}
