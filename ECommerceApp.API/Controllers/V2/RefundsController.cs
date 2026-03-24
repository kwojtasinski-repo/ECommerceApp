using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/refunds")]
    public class RefundsController : BaseController
    {
        private readonly IRefundService _refunds;

        public RefundsController(IRefundService refunds)
        {
            _refunds = refunds;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetRefund(int id, CancellationToken ct = default)
        {
            var vm = await _refunds.GetRefundAsync(id, ct);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost]
        public async Task<IActionResult> RequestRefund(
            [FromBody] RequestRefundDto dto,
            CancellationToken ct = default)
        {
            var result = await _refunds.RequestRefundAsync(dto, ct);
            return result switch
            {
                RefundRequestResult.Requested => StatusCode(StatusCodes.Status201Created),
                RefundRequestResult.OrderNotFound => NotFound(new { error = "Order not found." }),
                RefundRequestResult.RefundAlreadyExists => Conflict(new { error = "An active refund already exists for this order." }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
