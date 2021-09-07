using ECommerceApp.Application.Services;
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
        private readonly ItemServiceAbstract _itemService;

        public BrandController(ItemServiceAbstract itemService)
        {
            _itemService = itemService;
        }

        [HttpGet]
        public ActionResult<List<BrandForListVm>> GetItemBrands()
        {
            var brands = _itemService.GetAllItemBrands();
            if (brands.Count == 0)
            {
                return NotFound();
            }
            return Ok(brands);
        }

        [HttpGet("{id}")]
        public ActionResult<NewItemBrandVm> GetBrand(int id)
        {
            var brand = _itemService.GetItemBrandById(id);
            if (brand == null)
            {
                return NotFound();
            }
            return Ok(brand);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditItemBrand(NewItemBrandVm model)
        {
            var modelExists = _itemService.CheckIfItemBrandExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _itemService.UpdateItemBrand(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddItemBrand(NewItemBrandVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemBrand(model);
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItemBrand(int id)
        {
            _itemService.DeleteItemBrand(id);
            return RedirectToAction("Index");
        }
    }
}
