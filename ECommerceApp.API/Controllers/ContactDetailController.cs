using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/contact-details")]
    [ApiController]
    public class ContactDetailController : ControllerBase
    {
        private readonly IContactDetailService _contactDetailService;

        public ContactDetailController(IContactDetailService contactDetailService)
        {
            _contactDetailService = contactDetailService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("{id}")]
        public ActionResult<ContactDetailsForListVm> GetContactDetail(int id)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var contactDetail = _contactDetailService.GetContactDetails(id, userId);
            if (contactDetail == null)
            {
                return NotFound();
            }
            return Ok(contactDetail);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPut]
        public IActionResult EditContactDetail(ContactDetailVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var modelExists = _contactDetailService.ContactDetailExists(model.Id, userId);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _contactDetailService.UpdateContactDetail(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddContactDetail([FromBody] ContactDetailVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }

            var id = _contactDetailService.AddContactDetail(model, userId);

            if (id == 0)
            {
                return NotFound();
            }

            return Ok();
        }
    }
}
