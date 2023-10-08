using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace ECommerceApp.API.Controllers
{
    [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
    [Route("api/contact-details")]
    [ApiController]
    public class ContactDetailController : ControllerBase
    {
        private readonly IContactDetailService _contactDetailService;

        public ContactDetailController(IContactDetailService contactDetailService)
        {
            _contactDetailService = contactDetailService;
        }

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

            return Ok(id);
        }
    }
}
