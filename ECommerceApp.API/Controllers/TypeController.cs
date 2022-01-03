using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Type;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/types")]
    [ApiController]
    public class TypeController : ControllerBase
    {
        private readonly ITypeService _typeService;

        public TypeController(ITypeService typeService)
        {
            _typeService = typeService;
        }

        [HttpGet]
        public ActionResult<List<TypeVm>> GetItemTypes()
        {
            var types = _typeService.GetTypes(t => true);
            if (types.Count() == 0)
            {
                return NotFound();
            }
            return Ok(types);
        }

        [HttpGet("{id}")]
        public ActionResult<TypeVm> GetType(int id)
        {
            var type = _typeService.GetTypeDetails(id);
            if (type == null)
            {
                return NotFound();
            }
            return Ok(type);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditItemType(TypeVm model)
        {
            var modelExists = _typeService.TypeExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _typeService.UpdateType(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddItemType(TypeVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var id = _typeService.AddType(model);
            return Ok(id);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItemType(int id)
        {
            _typeService.DeleteType(id);
            return Ok();
        }
    }
}
