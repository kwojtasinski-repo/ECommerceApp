using ECommerceApp.Application.Sales.Payments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.Sales
{
    [Authorize]
    [Route("api/payments")]
    public class PaymentsController : BaseController
    {
        private readonly IPaymentService _payments;

        public PaymentsController(IPaymentService payments)
        {
            _payments = payments;
        }

        [HttpGet("{paymentId:guid}")]
        public async Task<IActionResult> GetByToken(Guid paymentId, CancellationToken ct = default)
        {
            var userId = GetUserId();
            var vm = await _payments.GetByTokenAsync(paymentId, userId, ct);
            return vm is null ? NotFound() : Ok(vm);
        }
    }
}
