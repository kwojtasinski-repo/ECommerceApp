using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Customer;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = _customerService.GetAllCustomersForList(10, 1, "");

            return View(model);
        }

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
            return View(new NewCustomerVm());
        }

        [HttpPost]
        public IActionResult AddCustomer(NewCustomerVm model)
        {
            var id = _customerService.AddCustomer(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult AddNewAddressForClient(int id)
        {
            ViewBag.CustomerId = id;
            return View();
        }

        [HttpPost]
        public IActionResult AddNewAddressForClient(NewAddressVm newAddress)
        {
            _customerService.CreateNewAddress(newAddress);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult AddNewContactDetailClient(int id)
        {
            ViewBag.CustomerId = id;
            ViewBag.ContactDetailTypes = _customerService.GetConactDetailTypes().ToList();
            return View();
        }

        [HttpPost]
        public IActionResult AddNewContactDetailClient(NewContactDetailVm newContact)
        {
            _customerService.CreateNewDetailContact(newContact);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditAddress(int id)
        {
            var customer = _customerService.GetAddressForEdit(id);
            return View(customer);
        }

        [HttpPost]
        public IActionResult EditAddress(NewAddressVm model)
        {
            _customerService.UpdateAddress(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditContactDetail(int id)
        {
            var contactDetail = _customerService.GetContactDetail(id);
            contactDetail.ContactDetailTypes = _customerService.GetConactDetailTypes().ToList();
            return View(contactDetail);
        }

        [HttpPost]
        public IActionResult EditContactDetail(NewContactDetailVm model)
        {
            _customerService.UpdateContactDetail(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditCustomer(int id)
        {
            var customer = _customerService.GetCustomerForEdit(id);
            return View(customer);
        }

        [HttpPost]
        public IActionResult EditCustomer(NewCustomerVm model)
        {
            _customerService.UpdateCustomer(model);
            return RedirectToAction("Index");
        }

        public IActionResult ViewCustomer(int id)
        {
            var customer = _customerService.GetCustomerDetails(id);
            return View(customer);
        }

        public IActionResult ViewAddress(int id)
        {
            var address = _customerService.GetAddressDetail(id);
            return View(address);
        }

        public IActionResult ViewContactDetail(int id)
        {
            var contactDetail = _customerService.GetContactDetail(id);
            return View(contactDetail);
        }

        public IActionResult Delete(int id)
        {
            _customerService.DeleteCustomer(id);
            return RedirectToAction("Index");
        }

        public IActionResult DeleteAddress(int id)
        {
            _customerService.DeleteAddress(id);
            return RedirectToAction("Index");
        }

        public IActionResult DeleteContactDetail(int id)
        {
            _customerService.DeleteContactDetail(id);
            return RedirectToAction("Index");
        }
    }
}
