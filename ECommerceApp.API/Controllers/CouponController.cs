﻿using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Route("api/coupons")]
    [Authorize]
    [ApiController]
    public class CouponController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public CouponController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public ActionResult<ListForCouponVm> Index([FromQuery] int pageSize = 20, int pageNo = 1, string searchString = "")
        {
            var coupons = _couponService.GetAllCoupons(pageSize, pageNo, searchString);

            if (coupons.Coupons.Count == 0)
            {
                return NotFound();
            }
            return coupons;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public ActionResult<int> AddCoupon(CouponVm couponVm)
        {
            var id = _couponService.AddCoupon(couponVm);
            return id;            
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPut]
        public IActionResult EditCoupon(CouponVm model)
        {
            var coupon = _couponService.Get(model.Id);
            
            if(coupon == null)
            {
                return Conflict();
            }

            _couponService.UpdateCoupon(model);
            return Ok();
        }


        [HttpGet("{id}")]
        public ActionResult<CouponDetailsVm> ViewCoupon(int id)
        {
            var coupon = _couponService.GetCouponDetail(id);
            
            if(coupon == null)
            {
                return NotFound();
            }

            return coupon;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteCoupon(int id)
        {
            _couponService.DeleteCoupon(id);
            return Ok();
        }
    }
}
