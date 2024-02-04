using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Application.ViewModels.ContactDetail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/contact-details")]
    public class ContactDetailController : BaseController
    {
        private readonly IContactDetailService _contactDetailService;

        public ContactDetailController(IContactDetailService contactDetailService)
        {
            _contactDetailService = contactDetailService;
        }

        [HttpGet("{id}")]
        public ActionResult<ContactDetailsForListVm> GetContactDetail(int id)
        {
            var contactDetail = _contactDetailService.GetContactDetail(id);
            if (contactDetail == null)
            {
                return NotFound();
            }
            return Ok(contactDetail);
        }

        [HttpPut("{id:int}")]
        public IActionResult EditContactDetail(int id, ContactDetailDto model)
        {
            model.Id = id;
            return _contactDetailService.UpdateContactDetail(model) 
                ? Ok()
                : NotFound();
        }

        [HttpPost]
        public IActionResult AddContactDetail([FromBody] ContactDetailDto model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }

            var id = _contactDetailService.AddContactDetail(model);
            return Ok(id);
        }
    }
}
