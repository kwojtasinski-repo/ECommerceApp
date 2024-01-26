using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Brands;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.API.Controllers
{
    [Route("api/brands")]
    [ApiController]
    public class BrandController : ControllerBase
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
            if (!brands.Any())
            {
                return NotFound();
            }
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPut]
        public IActionResult EditBrand(BrandDto model)
        {
            var modelExists = _brandService.BrandExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _brandService.UpdateBrand(model);
            return Ok();
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteBrand(int id)
        {
            _brandService.DeleteBrand(id);
            return Ok();
        }
    }
}
