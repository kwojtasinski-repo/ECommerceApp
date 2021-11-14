using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Payment;
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
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet]
        public ActionResult<List<PaymentVm>> GetPayments()
        {
            var payments = _paymentService.GetPayments(p => true);
            if (payments.Count() == 0)
            {
                return NotFound();
            }
            return Ok(payments);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("{id}")]
        public ActionResult<PaymentDetailsVm> GetPayment(int id)
        {
            var payment = _paymentService.GetPaymentDetails(id);
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
            var payment = _paymentService.InitPayment(model.OrderId);
            var id = _paymentService.AddPayment(payment);
            return Ok(id);
        }
    }
}
