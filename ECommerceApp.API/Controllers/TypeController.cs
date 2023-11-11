using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteItemType(int id)
        {
            _typeService.DeleteType(id);
            return Ok();
        }
    }
}
