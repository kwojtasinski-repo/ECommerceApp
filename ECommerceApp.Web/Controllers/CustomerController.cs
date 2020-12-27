using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ECommerceApp.Web.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _customerService.GetAllCustomersForList(10, 1, "");

            return View(model);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var model = _customerService.GetAllCustomersForList(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [HttpGet]
        public IActionResult AddCustomer()
        {
            var customer = new NewCustomerVm();
            if (User.Identity.IsAuthenticated)
            {
                customer.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }
            else
            {
                return Redirect("~/Identity/Account/Register");
            }
            return View(customer);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddCustomer(NewCustomerVm model)
        {
            var id = _customerService.AddCustomer(model);
            return Redirect("~/");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddNewAddressForClient(int id)
        {
            ViewBag.CustomerId = id;
            return View();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddNewAddressForClient(NewAddressVm newAddress)
        {
            _customerService.CreateNewAddress(newAddress);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddNewContactDetailClient(int id)
        {
            ViewBag.CustomerId = id;
            ViewBag.ContactDetailTypes = _customerService.GetConactDetailTypes().ToList();
            return View();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddNewContactDetailClient(NewContactDetailVm newContact)
        {
            _customerService.CreateNewDetailContact(newContact);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult EditAddress(int id)
        {
            var customer = _customerService.GetAddressForEdit(id);
            return View(customer);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult EditAddress(NewAddressVm model)
        {
            _customerService.UpdateAddress(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult EditContactDetail(int id)
        {
            var contactDetail = _customerService.GetContactDetail(id);
            contactDetail.ContactDetailTypes = _customerService.GetConactDetailTypes().ToList();
            return View(contactDetail);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult EditContactDetail(NewContactDetailVm model)
        {
            _customerService.UpdateContactDetail(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult EditCustomer(int id)
        {
            var customer = _customerService.GetCustomerForEdit(id);
            return View(customer);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult EditCustomer(NewCustomerVm model)
        {
            _customerService.UpdateCustomer(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewCustomer(int id)
        {
            var customer = _customerService.GetCustomerDetails(id);
            return View(customer);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewAddress(int id)
        {
            var address = _customerService.GetAddressDetail(id);
            return View(address);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewContactDetail(int id)
        {
            var contactDetail = _customerService.GetContactDetail(id);
            return View(contactDetail);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult Delete(int id)
        {
            _customerService.DeleteCustomer(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult DeleteAddress(int id)
        {
            _customerService.DeleteAddress(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult DeleteContactDetail(int id)
        {
            _customerService.DeleteContactDetail(id);
            return RedirectToAction("Index");
        }
    }
}
