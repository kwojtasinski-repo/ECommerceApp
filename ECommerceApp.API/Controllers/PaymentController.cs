using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/payments")]
    public class PaymentController : BaseController
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [Authorize(Roles = $"{ManagingRole}")]
        [HttpGet]
        public ActionResult<List<PaymentDto>> GetPayments()
        {
            var payments = _paymentService.GetPayments();
            return Ok(payments);
        }

        [HttpGet("{id}")]
        public ActionResult<PaymentDetailsDto> GetPayment(int id)
        {
            var payment = _paymentService.GetPaymentDetails(id);
            if (payment == null)
            {
                return NotFound();
            }
            return Ok(payment);
        }

        [HttpPost]
        public ActionResult<int> AddPayment([FromBody] AddPaymentDto model)
        {
            if (!ModelState.IsValid)
            {
                return Conflict(ModelState);
            }
            var id = _paymentService.AddPayment(model);
            return Ok(id);
        }
    }
}
