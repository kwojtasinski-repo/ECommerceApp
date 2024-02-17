using ECommerceApp.Application.ViewModels.ContactDetail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.DTO;
using System.Collections.Generic;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class ContactDetailController : BaseController
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
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", MapExceptionAsRouteValues(ex, new Dictionary<string, object> { { "Id", newContact.ContactDetail.CustomerId } } ));
            }
        }

        [HttpGet]
        public IActionResult EditContactDetail(int id)
        {
            var contactDetail = _contactDetailService.GetContactDetailById(id);
            if (contactDetail is null)
            {
                var errorModel = BuildErrorModel("contactDetailNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new NewContactDetailVm { ContactDetail = new ContactDetailDto() });
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
                    var errorModel = BuildErrorModel(ErrorCode.Create("contactDetailNotFound", ErrorParameter.Create("id", model.ContactDetail.CustomerId)));
                    return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", errorModel.AsOjectRoute(new Dictionary<string, object> { { "Id", model.ContactDetail.CustomerId } }));
                }

                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.ContactDetail.CustomerId });
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", MapExceptionAsRouteValues(ex, new Dictionary<string, object> { { "Id", model.ContactDetail.Id } }));
            }
        }

        public IActionResult ViewContactDetail(int id)
        {
            var contactDetail = _contactDetailService.GetContactDetail(id);
            if (contactDetail is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("contactDetailNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new ContactDetailsForListVm());
            }

            return View(contactDetail);
        }

        public IActionResult DeleteContactDetail(int id)
        {
            try
            {
                if (!_contactDetailService.DeleteContactDetail(id))
                {
                    return NotFound();
                }

                return Json(new { Success = true });
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }
    }
}
