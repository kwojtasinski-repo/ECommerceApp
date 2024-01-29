using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.ContactDetails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.API.Controllers
{
    [Authorize(Roles = $"{MaintenanceRole}")]
    [Route("api/contact-detail-types")]
    public class ContactDetailTypeController : BaseController
    {
        private readonly IContactDetailTypeService _contactDetailTypeService;

        public ContactDetailTypeController(IContactDetailTypeService contactDetailTypeService)
        {
            _contactDetailTypeService = contactDetailTypeService;
        }

        [HttpGet("{id}")]
        public ActionResult<ContactDetailTypeDto> GetContactDetailType(int id)
        {
            var contactDetailType = _contactDetailTypeService.GetContactDetailType(id);
            if (contactDetailType == null)
            {
                return NotFound();
            }
            return Ok(contactDetailType);
        }

        [HttpGet]
        public ActionResult<List<ContactDetailTypeDto>> GetContactDetailTypes()
        {
            return _contactDetailTypeService.GetContactDetailTypes().ToList();
        }

        [HttpPut]
        public IActionResult EditContactDetailType(ContactDetailTypeDto model)
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
        public IActionResult AddContactDetailType([FromBody] ContactDetailTypeDto model)
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
