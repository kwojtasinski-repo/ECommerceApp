using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize(Roles = MaintenanceRole)]
    public class CouponController : BaseController
    {
        private readonly ICouponService _couponService;

        public CouponController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _couponService.GetCouponsAsync(20, 1, string.Empty);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string? searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var model = await _couponService.GetCouponsAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateCouponDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCouponDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Description))
            {
                ModelState.AddModelError(string.Empty, "Kod i opis są wymagane.");
                return View(dto);
            }

            var added = await _couponService.AddCouponAsync(dto.Code, dto.Description);
            if (!added)
            {
                ModelState.AddModelError(string.Empty, $"Kupon z kodem '{dto.Code}' już istnieje.");
                return View(dto);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var coupon = await _couponService.GetCouponAsync(id);
            if (coupon is null)
                return NotFound();
            return View(new UpdateCouponDto { Id = coupon.Id, Code = coupon.Code, Description = coupon.Description });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(UpdateCouponDto dto)
        {
            if (!await _couponService.UpdateCouponAsync(dto))
                return NotFound();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var coupon = await _couponService.GetCouponAsync(id);
            if (coupon is null)
                return NotFound();
            return View(coupon);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _couponService.DeleteCouponAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
