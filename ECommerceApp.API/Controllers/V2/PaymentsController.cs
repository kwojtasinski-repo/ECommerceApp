using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/v2/payments")]
    public class PaymentsController : BaseController
    {
        private readonly IPaymentService _payments;

        public PaymentsController(IPaymentService payments)
        {
            _payments = payments;
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
        {
            var vm = await _payments.GetByIdAsync(id, ct);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpGet("by-order/{orderId:int}")]
        public async Task<IActionResult> GetByOrderId(int orderId, CancellationToken ct = default)
        {
            var vm = await _payments.GetByOrderIdAsync(orderId, ct);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPut("confirm")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Confirm([FromBody] ConfirmPaymentDto dto, CancellationToken ct = default)
        {
            var result = await _payments.ConfirmAsync(dto, ct);
            return result switch
            {
                PaymentOperationResult.Success => Ok(),
                PaymentOperationResult.PaymentNotFound => NotFound(new { error = "Payment not found." }),
                PaymentOperationResult.AlreadyConfirmed => Conflict(new { error = "Payment is already confirmed." }),
                PaymentOperationResult.AlreadyExpired => Conflict(new { error = "Payment window has expired." }),
                PaymentOperationResult.AlreadyRefunded => Conflict(new { error = "Payment has already been refunded." }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
