using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.API.Controllers
{
    [Route("api/types")]
    public class TypeController : BaseController
    {
        private readonly ITypeService _typeService;

        public TypeController(ITypeService typeService)
        {
            _typeService = typeService;
        }

        [HttpGet]
        public ActionResult<List<TypeDto>> GetItemTypes()
        {
            return Ok(_typeService.GetTypes());
        }

        [HttpGet("{id}")]
        public ActionResult<TypeDto> GetType(int id)
        {
            var type = _typeService.GetTypeById(id);
            if (type == null)
            {
                return NotFound();
            }
            return Ok(type);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut("{id:int}")]
        public IActionResult EditItemType(int id, TypeDto model)
        {
            model.Id = id;
            if (!ModelState.IsValid)
            {
                return Conflict(ModelState);
            }
            return _typeService.UpdateType(model)
                ? Ok()
                : NotFound();
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult AddItemType(TypeDto model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var id = _typeService.AddType(model);
            return Ok(id);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteItemType(int id)
        {
            return _typeService.DeleteType(id)
                ? Ok()
                : NotFound();
        }
    }
}
