using System.Linq;
using System.Security.Claims;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/customers")]
    public class CustomerController : BaseController
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
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

        [HttpGet("{id}")]
        public ActionResult<CustomerDetailsVm> GetCustomer(int id)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            var customer = _customerService.GetCustomerDetails(id, userId);
            if (customer == null)
            {
                return NotFound();
            }
            return Ok(customer);
        }

        [HttpPut("{id:int}")]
        public IActionResult EditCustomer(int id, CustomerDto model)
        {
            model.Id = id;
            if (!ModelState.IsValid)
            {
                return Conflict(ModelState);
            }
            return _customerService.UpdateCustomer(model)
                ? Ok()
                : NotFound();
        }

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
