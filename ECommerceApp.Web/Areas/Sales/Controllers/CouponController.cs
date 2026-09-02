using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize(Roles = MaintenanceRole)]
    public class CouponController : BaseController
    {
        private readonly ICouponService _couponService;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IProductTagService _productTagService;

        public CouponController(
            ICouponService couponService,
            IProductService productService,
            ICategoryService categoryService,
            IProductTagService productTagService)
        {
            _couponService = couponService;
            _productService = productService;
            _categoryService = categoryService;
            _productTagService = productTagService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _couponService.GetCouponsAsync(20, 1, string.Empty);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            var model = await _couponService.GetCouponsAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateRuleBuilderAsync();
            return View(new CreateCouponDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCouponDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Description))
            {
                ModelState.AddModelError(string.Empty, "Kod i opis są wymagane.");
                await PopulateRuleBuilderAsync();
                return View(dto);
            }

            var result = await _couponService.CreateCouponAsync(dto);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.FailureReason);
                await PopulateRuleBuilderAsync();
                return View(dto);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ScopeTargets(string scopeType, string query = "")
        {
            if (scopeType == "per-product")
            {
                // #scopeTargetSelect is a plain <select>, not a paginated/searchable combobox (see
                // coupon-rule-builder.js's loadTargets): it's populated once with whatever this call
                // returns and offers no "next page" affordance, so a small page size silently makes every
                // product past it permanently unreachable from this picker. Real SQL Server testing (and
                // this endpoint's page size in particular) is what surfaced this — InMemory tests never
                // seeded more than a handful of products, so it always fit on page 1.
                var products = await _productService.GetAllProducts(500, 1, query ?? string.Empty);
                return Json(products.Products.Select(p => new { id = p.Id, name = p.Name }));
            }

            if (scopeType == "per-category")
            {
                var categories = await _categoryService.GetAllCategories();
                return Json(categories
                    .Where(c => string.IsNullOrWhiteSpace(query) || c.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase))
                    .Select(c => new { id = c.Id, name = c.Name }));
            }

            if (scopeType == "per-tag")
            {
                var tags = await _productTagService.SearchTags(query ?? string.Empty, 10);
                return Json(tags.Select(t => new { id = t.Id, name = t.Name }));
            }

            return BadRequest("Nieprawidłowy typ zakresu.");
        }

        private async Task PopulateRuleBuilderAsync()
        {
            ViewBag.RuleCatalog = _couponService.GetRuleCatalog();
            ViewBag.ScopeTargetUrl = Url.Action(nameof(ScopeTargets));
            ViewBag.Categories = await _categoryService.GetAllCategories();
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var coupon = await _couponService.GetCouponAsync(id);
            if (coupon is null)
                return NotFound();
            await PopulateRuleBuilderAsync();
            return View(new UpdateCouponDto
            {
                Id = coupon.Id,
                Code = coupon.Code,
                Description = coupon.Description,
                Rules = ToRulesForm(coupon.Rules),
                ScopeTargets = coupon.ScopeTargets.ToList(),
                Version = coupon.Version
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(UpdateCouponDto dto)
        {
            var result = await _couponService.UpdateCouponAsync(dto);
            if (result.Outcome == CouponUpdateOutcome.NotFound)
            {
                return NotFound();
            }

            if (result.Outcome == CouponUpdateOutcome.ConcurrencyConflict)
            {
                ModelState.AddModelError(string.Empty, "Kupon został zmieniony przez inną osobę. Odśwież formularz i spróbuj ponownie.");
                var current = await _couponService.GetCouponAsync(dto.Id);
                if (current is null)
                {
                    return NotFound();
                }

                dto.Code = current.Code;
                dto.Description = current.Description;
                dto.Rules = ToRulesForm(current.Rules);
                dto.ScopeTargets = current.ScopeTargets.ToList();
                dto.Version = current.Version;
            }
            else if (result.Outcome == CouponUpdateOutcome.ValidationFailed)
            {
                ModelState.AddModelError(string.Empty, result.FailureReason);
            }
            else
            {
                return RedirectToAction(nameof(Index));
            }

            await PopulateRuleBuilderAsync();
            return View(dto);
        }

        private static CouponRulesFormDto ToRulesForm(IEnumerable<CouponRuleDefinition> rules)
        {
            var form = new CouponRulesFormDto();
            foreach (var rule in rules)
            {
                var selection = new RuleSelectionDto
                {
                    Name = rule.Name,
                    Parameters = new System.Collections.Generic.Dictionary<string, string>(rule.Parameters)
                };

                if (rule.Category == CouponRuleCategory.Scope)
                {
                    form.Scope = selection;
                }
                else if (rule.Category == CouponRuleCategory.Discount)
                {
                    form.Discount = selection;
                }
                else if (rule.Category == CouponRuleCategory.Constraint)
                {
                    form.Constraints.Add(selection);
                }
            }

            return form;
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
