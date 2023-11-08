using System;
using System.Collections.Generic;
using System.Security.Claims;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class CustomerController : BaseController
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = _customerService.GetAllCustomersForList(userId, 10, 1, "");

            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            var model = _customerService.GetAllCustomersForList(userId, pageSize, pageNo.Value, searchString ?? string.Empty);

            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult All()
        {
            var model = _customerService.GetAllCustomersForList(10, 1, "");
            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult All(int pageSize, int? pageNo, string searchString)
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
        [Authorize]
        public IActionResult AddCustomer()
        {
            var customer = new NewCustomerVm() { Addresses = new List<AddressVm> { new AddressVm()}, ContactDetails = new List<NewContactDetailVm> { new NewContactDetailVm() } };
            customer.UserId = GetUserId().Value;
            return View(customer);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddCustomer(NewCustomerVm model)
        {
            var id = _customerService.AddCustomer(model);
            return Redirect("~/");
        }

        [Authorize]
        public IActionResult AddCustomerPartialView()
        {
            var customer = new NewCustomerVm();
            customer.UserId = GetUserId().Value;
            return PartialView(customer);
        }

        [Authorize]
        [HttpGet]
        public IActionResult EditCustomer(int id)
        {
            // TODO return null from backend
            var customer = _customerService.GetCustomerForEdit(id);
            var userId = GetUserId();
            var role = GetUserRole();
            if (userId.Value != customer.UserId && role.Value == UserPermissions.Roles.User)
            {
                return Forbid();
            }
            return View(customer);
        }

        [Authorize]
        [HttpPost]
        public IActionResult EditCustomer(NewCustomerVm model)
        {
            // TODO check if user has rights on backend to this customer
            var userId = GetUserId();
            var role = GetUserRole();
            if (userId.Value != model.UserId && role.Value == UserPermissions.Roles.User)
            {
                return Forbid();
            }
            _customerService.UpdateCustomer(model);
            return RedirectToAction("Index");
        }


        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        public IActionResult ViewCustomer(int id)
        {
            // TODO check if user has rights on backend to this customer
            var customer = _customerService.GetCustomerDetails(id);
            return View(customer);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        public IActionResult Delete(int id)
        {
            // TODO check if user has rights on backend to this customer
            _customerService.DeleteCustomer(id);
            return Json("deleted");
        }
    }
}
