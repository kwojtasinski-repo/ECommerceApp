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
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "AddNewContactDetail", controllerName: "ContactDetail", new { newContact.ContactDetail.CustomerId, Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
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
                    var errorModel = BuildErrorModel("contactDetailNotFound", new Dictionary<string, string> { { "id", $"{model.ContactDetail.CustomerId}" } });
                    HttpContext.Request.Query = errorModel.AsQueryCollection();
                    return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.ContactDetail.CustomerId, Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
                }

                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.ContactDetail.CustomerId });
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "EditContactDetail", controllerName: "ContactDetail", new { model.ContactDetail.Id, Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        public IActionResult ViewContactDetail(int id)
        {
            var contactDetail = _contactDetailService.GetContactDetail(id);
            if (contactDetail is null)
            {
                var errorModel = BuildErrorModel("contactDetailNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
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
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
