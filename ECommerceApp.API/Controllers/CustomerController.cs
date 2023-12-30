using System.Linq;
using System.Security.Claims;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Route("api/customers")]
    [Authorize]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}")]
        [HttpGet]
        public ActionResult<ListForCustomerVm> GetCustomers([FromQuery] int pageSize = 10, int pageNo = 1, string searchString = "")
        {
            var customers = _customerService.GetAllCustomersForList(pageSize, pageNo, searchString);

            if (customers.Customers.Count == 0)
            {
                return NotFound();
            }
            return Ok(customers);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPut]
        public IActionResult EditCustomer(CustomerDto model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var modelExists = _customerService.CustomerExists(model.Id, userId);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _customerService.UpdateCustomer(model);
            return Ok();
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddCustomer([FromBody] CustomerDto model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var id = _customerService.AddCustomer(model);
            return Ok(id);
        }
    }
}
