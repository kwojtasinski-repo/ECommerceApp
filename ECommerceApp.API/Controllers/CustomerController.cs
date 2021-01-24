using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly CustomerServiceAbstract _customerService;

        public CustomerController(CustomerServiceAbstract customerService)
        {
            _customerService = customerService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet("Customer/All")]
        public ActionResult<List<CustomerForListVm>> GetCustomers()
        {
            var customers = _customerService.GetAllCustomersForList();
            if (customers.Count == 0)
            {
                return NotFound();
            }
            return Ok(customers);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("ContactDetailType/All")]
        public ActionResult<List<ContactDetailTypeVm>> GetContactDetailTypes()
        {
            var contactDetailTypes = _customerService.GetConactDetailTypes().ToList();
            if (contactDetailTypes.Count == 0)
            {
                return NotFound();
            }
            return Ok(contactDetailTypes);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("Customer/Get/{id}")]
        public ActionResult<CustomerDetailsVm> GetCustomer(int id)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var customer = _customerService.GetCustomerDetails(id, userId);
            if (customer == null)
            {
                return NotFound();
            }
            return Ok(customer);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("Address/Get/{id}")]
        public ActionResult<AddressDetailVm> GetAddress(int id)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var address = _customerService.GetAddressDetail(id, userId);
            if (address == null)
            {
                return NotFound();
            }
            return Ok(address);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("ContactDetail/Get/{id}")]
        public ActionResult<NewContactDetailVm> GetContactDetail(int id)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var contactDetail = _customerService.GetContactDetail(id, userId);
            if (contactDetail == null)
            {
                return NotFound();
            }
            return Ok(contactDetail);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("ContactDetailType/Get/{id}")]
        public ActionResult<NewContactDetailTypeVm> GetContactDetailType(int id)
        {
            var contactDetailType = _customerService.GetContactDetailType(id);
            if (contactDetailType == null)
            {
                return NotFound();
            }
            return Ok(contactDetailType);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPut("Customer/Edit/{id}")]
        public IActionResult EditCustomer(NewCustomerVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var modelExists = _customerService.CheckIfCustomerExists(model.Id, userId);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _customerService.UpdateCustomer(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPut("Address/Edit/{id}")]
        public IActionResult EditAddress([FromBody]NewAddressVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var modelExists = _customerService.CheckIfAddressExists(model.Id, userId);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _customerService.UpdateAddress(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPut("ContactDetail/Edit/{id}")]
        public IActionResult EditContactDetail(NewContactDetailVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var modelExists = _customerService.CheckIfContactDetailExists(model.Id, userId);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _customerService.UpdateContactDetail(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut("ContactDetailType/Edit/{id}")]
        public IActionResult EditContactDetailType(NewContactDetailTypeVm model)
        {
            var modelExists = _customerService.CheckIfContactDetailType(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _customerService.UpdateContactDetailType(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("Customer/New")]
        public IActionResult AddCustomer([FromBody] NewCustomerVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _customerService.AddCustomer(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("Address/New")]
        public IActionResult AddAddress([FromBody] NewAddressVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            
            var id = _customerService.AddAddress(model, userId);
            
            if (id == 0)
            {
                return NotFound();
            }

            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("ContactDetail/New")]
        public IActionResult AddContactDetail([FromBody] NewContactDetailVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }

            var id = _customerService.AddContactDetail(model, userId);

            if (id == 0)
            {
                return NotFound();
            }

            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("ContactDetailType/New")]
        public IActionResult AddContactDetailType([FromBody] NewContactDetailTypeVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _customerService.AddContactDetailType(model);
            return Ok();
        }
    }
}
