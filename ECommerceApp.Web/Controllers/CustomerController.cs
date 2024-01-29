﻿using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class CustomerController : BaseController
    {
        private readonly ICustomerService _customerService;
        private readonly IContactDetailTypeService _contactDetailTypeService;

        public CustomerController(ICustomerService customerService, IContactDetailTypeService contactDetailTypeService)
        {
            _customerService = customerService;
            _contactDetailTypeService = contactDetailTypeService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = _customerService.GetAllCustomersForList(userId, 10, 1, "");

            return View(model);
        }

        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            var userId = GetUserId();
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            var model = _customerService.GetAllCustomersForList(userId, pageSize, pageNo.Value, searchString ?? string.Empty);

            return View(model);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult All()
        {
            var model = _customerService.GetAllCustomersForList(10, 1, "");
            return View(model);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult All(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var model = _customerService.GetAllCustomersForList(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public IActionResult AddCustomer()
        {
            var customer = new CustomerVm
            {
                Customer = new CustomerDto { UserId = GetUserId() },
                ContactDetailTypes = new List<ContactDetailTypeDto>(),
                ContactDetails = new List<ContactDetailDto>(),
                Addresses = new List<AddressDto>()
            };
            return View(customer);
        }

        [HttpPost]
        public IActionResult AddCustomer(CustomerVm model)
        {
            _customerService.AddCustomerDetails(new CustomerDetailsDto
            {
                Id = model.Customer.Id,
                FirstName = model.Customer.FirstName,
                LastName = model.Customer.LastName,
                IsCompany = model.Customer.IsCompany,
                CompanyName = model.Customer.CompanyName,
                NIP = model.Customer.NIP,
                UserId = model.Customer.UserId,
                Addresses = model.Addresses,
                ContactDetails = model.ContactDetails
            });
            return RedirectToAction("Index");
        }

        public IActionResult AddCustomerPartialView()
        {
            var customer = new CustomerVm
            {
                Customer = new CustomerDto { UserId = GetUserId() },
                ContactDetailTypes = new List<ContactDetailTypeDto>(),
                ContactDetails = new List<ContactDetailDto>(),
                Addresses = new List<AddressDto>()
            };
            return PartialView(customer);
        }

        [HttpGet]
        public IActionResult EditCustomer(int id)
        {
            // TODO return null from backend
            var customer = _customerService.GetCustomer(id);
            if (customer is null)
            {
                return NotFound();
            }

            var userId = GetUserId();
            var role = GetUserRole();
            if (userId != customer.UserId && role == UserPermissions.Roles.User)
            {
                return Forbid();
            }
            var vm = new CustomerVm
            {
                Customer = new CustomerDto
                {
                    Id = id,
                    FirstName = customer.FirstName,
                    LastName = customer.LastName,
                    IsCompany = customer.IsCompany,
                    CompanyName = customer.CompanyName,
                    NIP = customer.NIP,
                    UserId = customer.UserId
                },
                Addresses = customer.Addresses,
                ContactDetails = customer.ContactDetails,
                ContactDetailTypes = _contactDetailTypeService.GetContactDetailTypes().ToList()
            };
            return View(vm);
        }

        [HttpPost]
        public IActionResult EditCustomer(CustomerVm model)
        {
            // TODO check if user has rights on backend to this customer
            var userId = GetUserId();
            var role = GetUserRole();
            if (userId != model.Customer.UserId && role == UserPermissions.Roles.User)
            {
                return Forbid();
            }
            _customerService.UpdateCustomer(model.Customer);
            return RedirectToAction("Index");
        }


        public IActionResult ViewCustomer(int id)
        {
            // TODO check if user has rights on backend to this customer
            var customer = _customerService.GetCustomerDetails(id);
            return View(customer);
        }

        public IActionResult Delete(int id)
        {
            // TODO check if user has rights on backend to this customer
            return _customerService.DeleteCustomer(id)
                ? Json("deleted") : NotFound();
        }

        [HttpGet]
        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        public IActionResult GetContacts(string userId)
        {
            return Ok(_customerService.GetCustomersInformationByUserId(userId));
        }
    }
}
