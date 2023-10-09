using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.ContactDetail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Application;
using System.Linq;
using ECommerceApp.Infrastructure.Permissions;

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
            ViewBag.CustomerId = id;
            ViewBag.ContactDetailTypes = _contactDetailTypeService.GetContactDetailTypes(cdt => true);
            return View();
        }

        [HttpPost]
        public IActionResult AddNewContactDetail(NewContactDetailVm newContact)
        {
            _contactDetailService.AddContactDetail(newContact.AsContactDetailVm());
            return RedirectToAction(actionName: "Index", controllerName: "Customer");
        }

        [HttpGet]
        public IActionResult EditContactDetail(int id)
        {
            var contactDetail = _contactDetailService.GetContactDetailById(id).AsNewContactDetailVm();
            contactDetail.ContactDetailTypes = _contactDetailTypeService.GetContactDetailTypes(cdt => true).ToList();
            return View(contactDetail);
        }

        [HttpPost]
        public IActionResult EditContactDetail(NewContactDetailVm model)
        {
            _contactDetailService.UpdateContactDetail(model.AsContactDetailVm());
            return RedirectToAction(actionName: "Index", controllerName: "Customer");
        }

        public IActionResult ViewContactDetail(int id)
        {
            var contactDetail = _contactDetailService.GetContactDetails(id);
            return View(contactDetail);
        }

        public IActionResult DeleteContactDetail(int id)
        {
            _contactDetailService.DeleteContactDetail(id);
            return RedirectToAction(actionName: "Index", controllerName: "Customer");
        }
    }
}
