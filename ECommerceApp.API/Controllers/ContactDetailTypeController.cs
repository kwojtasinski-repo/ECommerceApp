using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.API.Controllers
{
    [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
    [Route("api/contact-detail-types")]
    [ApiController]
    public class ContactDetailTypeController : ControllerBase
    {
        private readonly IContactDetailTypeService _contactDetailTypeService;

        public ContactDetailTypeController(IContactDetailTypeService contactDetailTypeService)
        {
            _contactDetailTypeService = contactDetailTypeService;
        }

        [HttpGet("{id}")]
        public ActionResult<ContactDetailTypeVm> GetContactDetailType(int id)
        {
            var contactDetailType = _contactDetailTypeService.GetContactDetailType(id);
            if (contactDetailType == null)
            {
                return NotFound();
            }
            return Ok(contactDetailType);
        }

        [HttpGet]
        public ActionResult<List<ContactDetailTypeVm>> GetContactDetailTypes()
        {
            var contactDetailTypes = _contactDetailTypeService.GetContactDetailTypes(c => true).ToList();
            if (contactDetailTypes.Count == 0)
            {
                return NotFound();
            }
            return Ok(contactDetailTypes);
        }

        [HttpPut]
        public IActionResult EditContactDetailType(ContactDetailTypeVm model)
        {
            var modelExists = _contactDetailTypeService.ContactDetailTypeExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _contactDetailTypeService.UpdateContactDetailType(model);
            return Ok();
        }

        [HttpPost]
        public IActionResult AddContactDetailType([FromBody] ContactDetailTypeVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _contactDetailTypeService.AddContactDetailType(model);
            return Ok();
        }
    }
}
