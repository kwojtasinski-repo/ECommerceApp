using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Brands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.API.Controllers
{
    [Route("api/brands")]
    public class BrandController : BaseController
    {
        private readonly IBrandService _brandService;

        public BrandController(IBrandService brandService)
        {
            _brandService = brandService;
        }

        [HttpGet]
        public ActionResult<List<BrandDto>> GetItemBrands()
        {
            var brands = _brandService.GetAllBrands();
            return Ok(brands);
        }

        [HttpGet("{id}")]
        public ActionResult<BrandDto> GetBrand(int id)
        {
            var brand = _brandService.GetBrand(id);
            if (brand == null)
            {
                return NotFound();
            }
            return Ok(brand);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut("{id:int}")]
        public IActionResult EditBrand(int id, BrandDto model)
        {
            model.Id = id;
            return _brandService.UpdateBrand(model)
                ? Ok()
                : NotFound();
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult AddBrand(BrandDto model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var id = _brandService.AddBrand(model);
            return Ok(id);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteBrand(int id)
        {
            _brandService.DeleteBrand(id);
            return Ok();
        }
    }
}
