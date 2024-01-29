using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Brands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class BrandController : BaseController
    {
        private readonly IBrandService _brandService;

        public BrandController(IBrandService brandService)
        {
            _brandService = brandService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var brand = _brandService.GetAllBrands(20, 1, "");
            return View(brand);
        }

        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var item = _brandService.GetAllBrands(pageSize, pageNo.Value, searchString);
            return View(item);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult AddBrand()
        {
            return View(new BrandDto());
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult AddBrand(BrandDto model)
        {
            try
            { 
                _brandService.AddBrand(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "AddBrand", controllerName: "Brand", new { Error = ex.Message });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult EditBrand(int id)
        {
            var brand = _brandService.GetBrand(id);
            if (brand is null)
            {
                return NotFound();
            }
            return View(brand);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult EditBrand(BrandDto model)
        {
            try
            {
                _brandService.UpdateBrand(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "EditBrand", controllerName: "Brand", new { model.Id, Error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ViewBrand(int id)
        {
            var brand = _brandService.GetBrand(id);
            if (brand is null)
            {
                return NotFound();
            }
            return View(brand);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeleteBrand(int id)
        {
            return _brandService.DeleteBrand(id)
                ? Json(new { Success = true })
                : NotFound();
        }
    }
}
