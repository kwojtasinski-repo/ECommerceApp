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
    [Route("api/customers")]
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
        [HttpGet("all")]
        public ActionResult<List<CustomerForListVm>> GetCustomers()
        {
            var customers = _customerService.GetAllCustomersForList();
            if (customers.Count == 0)
            {
                return NotFound();
            }
            return Ok(customers);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("{id}")]
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
        [HttpPut("{id}")]
        public IActionResult EditCustomer(CustomerForListVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var modelExists = _customerService.CheckIfCustomerExists(model.Id, userId);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            var customer = model.MapToNewCustomerVm();
            _customerService.UpdateCustomer(customer);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddCustomer([FromBody] CustomerForListVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var customer = model.MapToNewCustomerVm();
            _customerService.AddCustomer(customer);
            return Ok();
        }
    }
}
