using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.ViewModels.Coupon;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/coupons")]
    public class CouponController : BaseController
    {
        private readonly ICouponService _couponService;

        public CouponController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
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

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public ActionResult<int> AddCoupon(CouponVm couponVm)
        {
            var id = _couponService.AddCoupon(couponVm);
            return id;            
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut("{id:int}")]
        public IActionResult EditCoupon(int id, CouponVm model)
        {
            model.Id = id;
            return _couponService.UpdateCoupon(model)
                ? Ok()
                : NotFound();
        }


        [HttpGet("{id}")]
        public ActionResult<CouponDetailsVm> ViewCoupon(int id)
        {
            var coupon = _couponService.GetCouponDetail(id);
            
            if (coupon is null)
            {
                return NotFound();
            }

            return coupon;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteCoupon(int id)
        {
            return _couponService.DeleteCoupon(id)
                ? Ok()
                : NotFound();
        }
    }
}
