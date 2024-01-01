using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public ActionResult<int> AddPayment([FromBody] CreatePayment model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var id = _paymentService.AddPayment(new PaymentVm
            {
                CurrencyId = model.CurrencyId,
                OrderId = model.OrderId,
            });
            return Ok(id);
        }
    }
}
