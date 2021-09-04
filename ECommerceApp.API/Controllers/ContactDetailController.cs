using ECommerceApp.Application.Services;
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
        private readonly CustomerServiceAbstract _customerService;

        public ContactDetailController(CustomerServiceAbstract customerService)
        {
            _customerService = customerService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("{id}")]
        public ActionResult<NewContactDetailVm> GetContactDetail(int id)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var contactDetail = _customerService.GetContactDetail(id, userId);
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
            var modelExists = _customerService.CheckIfContactDetailExists(model.Id, userId);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            var contact = model.MapToNewContactDetail();
            _customerService.UpdateContactDetail(contact);
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

            var contact = model.MapToNewContactDetail();
            var id = _customerService.AddContactDetail(contact, userId);

            if (id == 0)
            {
                return NotFound();
            }

            return Ok();
        }
    }
}
