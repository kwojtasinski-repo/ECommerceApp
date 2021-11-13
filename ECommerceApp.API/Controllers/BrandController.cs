using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Application.ViewModels.Item;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public ActionResult<List<BrandVm>> GetItemBrands()
        {
            var brands = _brandService.GetAllBrands(b => true);
            if (brands.Count() == 0)
            {
                return NotFound();
            }
            return Ok(brands);
        }

        [HttpGet("{id}")]
        public ActionResult<BrandVm> GetBrand(int id)
        {
            var brand = _brandService.GetBrand(id);
            if (brand == null)
            {
                return NotFound();
            }
            return Ok(brand);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditBrand(BrandVm model)
        {
            var modelExists = _brandService.BrandExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _brandService.UpdateBrand(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddBrand(BrandVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _brandService.AddBrand(model);
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteBrand(int id)
        {
            _brandService.DeleteBrand(id);
            return RedirectToAction("Index");
        }
    }
}
