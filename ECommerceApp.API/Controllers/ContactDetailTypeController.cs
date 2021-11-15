using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/contact-detail-types")]
    [ApiController]
    public class ContactDetailTypeController : ControllerBase
    {
        private readonly IContactDetailTypeService _contactDetailTypeService;

        public ContactDetailTypeController(IContactDetailTypeService contactDetailTypeService)
        {
            _contactDetailTypeService = contactDetailTypeService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
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
