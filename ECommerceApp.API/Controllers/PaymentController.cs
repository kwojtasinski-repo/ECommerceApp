using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly OrderServiceAbstract _orderService;

        public PaymentController(OrderServiceAbstract orderService)
        {
            _orderService = orderService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet("all")]
        public ActionResult<List<PaymentForListVm>> GetPayments()
        {
            var payments = _orderService.GetAllPayments();
            if (payments.Count == 0)
            {
                return NotFound();
            }
            return Ok(payments);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("{id}")]
        public ActionResult<PaymentDetailsVm> GetPayment(int id)
        {
            var payment = _orderService.GetPaymentDetail(id);
            if (payment == null)
            {
                return NotFound();
            }
            return Ok(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public ActionResult<int> AddPayment([FromBody] CreatePayment model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var payment = _orderService.InitPayment(model.OrderId);
            var id = _orderService.AddPayment(payment);
            return Ok(id);
        }
    }
}
