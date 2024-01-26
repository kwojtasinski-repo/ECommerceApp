using ECommerceApp.Application.ViewModels.ContactDetail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using ECommerceApp.Infrastructure.Permissions;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.DTO;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
    public class ContactDetailController : Controller
    {
        private readonly IContactDetailService _contactDetailService;
        private readonly IContactDetailTypeService _contactDetailTypeService;

        public ContactDetailController(IContactDetailService contactDetailService, IContactDetailTypeService contactDetailTypeService)
        {
            _contactDetailService = contactDetailService;
            _contactDetailTypeService = contactDetailTypeService;
        }

        [HttpGet]
        public IActionResult AddNewContactDetail(int id)
        {
            return View(new NewContactDetailVm { 
                ContactDetail = new ContactDetailDto { CustomerId = id }, 
                ContactDetailTypes = _contactDetailTypeService.GetContactDetailTypes().ToList()
            });
        }

        [HttpPost]
        public IActionResult AddNewContactDetail(NewContactDetailVm newContact)
        {
            try
            {
                _contactDetailService.AddContactDetail(newContact.ContactDetail);
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = newContact.ContactDetail.CustomerId });
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "AddNewContactDetail", controllerName: "ContactDetail", new { newContact.ContactDetail.CustomerId, Error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult EditContactDetail(int id)
        {
            var contactDetail = _contactDetailService.GetContactDetailById(id);
            if (contactDetail is null)
            {
                return NotFound();
            }

            var vm = new NewContactDetailVm { ContactDetail = contactDetail, ContactDetailTypes = _contactDetailTypeService.GetContactDetailTypes().ToList() };
            return View(vm);
        }

        [HttpPost]
        public IActionResult EditContactDetail(NewContactDetailVm model)
        {
            try
            {
                if (!_contactDetailService.UpdateContactDetail(model.ContactDetail))
                {
                    return NotFound();
                }

                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.ContactDetail.CustomerId });
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "EditContactDetail", controllerName: "ContactDetail", new { model.ContactDetail.Id, Error = ex.Message });
            }
        }

        public IActionResult ViewContactDetail(int id)
        {
            var contactDetail = _contactDetailService.GetContactDetails(id);
            if (contactDetail is null)
            {
                return NotFound();
            }

            return View(contactDetail);
        }

        public IActionResult DeleteContactDetail(int id)
        {
            if (!_contactDetailService.DeleteContactDetail(id))
            {
                return NotFound();
            }

            return Json(new { Success = true });
        }
    }
}
