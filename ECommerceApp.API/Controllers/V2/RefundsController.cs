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
    [Route("api/v2/refunds")]
    public class RefundsController : BaseController
    {
        private readonly IRefundService _refunds;

        public RefundsController(IRefundService refunds)
        {
            _refunds = refunds;
        }

        /// <summary>
        /// Returns a paged list of all refunds. Requires maintenance role.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> GetRefunds(
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNo = 1,
            [FromQuery] string? search = null,
            CancellationToken ct = default)
        {
            var vm = await _refunds.GetRefundsAsync(pageSize, pageNo, search, ct);
            return Ok(vm);
        }

        /// <summary>
        /// Returns the details of a single refund by id.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetRefund(int id, CancellationToken ct = default)
        {
            var vm = await _refunds.GetRefundAsync(id, ct);
            return vm is null ? NotFound() : Ok(vm);
        }

        /// <summary>
        /// Submits a new refund request for an order.
        /// Returns 201 Created on success, 404 if the order does not exist,
        /// or 409 if an active refund already exists for the order.
        /// </summary>
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

        /// <summary>
        /// Approves a refund in Requested status. Requires maintenance role.
        /// Returns 200 on success, 404 if not found, or 409 if already processed.
        /// </summary>
        [HttpPut("{id:int}/approve")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> ApproveRefund(int id, CancellationToken ct = default)
        {
            var result = await _refunds.ApproveRefundAsync(id, ct);
            return result switch
            {
                RefundOperationResult.Success => Ok(),
                RefundOperationResult.RefundNotFound => NotFound(new { error = "Refund not found." }),
                RefundOperationResult.AlreadyProcessed => Conflict(new { error = "Refund has already been processed." }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        /// <summary>
        /// Rejects a refund in Requested status. Requires maintenance role.
        /// Returns 200 on success, 404 if not found, or 409 if already processed.
        /// </summary>
        [HttpPut("{id:int}/reject")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> RejectRefund(int id, CancellationToken ct = default)
        {
            var result = await _refunds.RejectRefundAsync(id, ct);
            return result switch
            {
                RefundOperationResult.Success => Ok(),
                RefundOperationResult.RefundNotFound => NotFound(new { error = "Refund not found." }),
                RefundOperationResult.AlreadyProcessed => Conflict(new { error = "Refund has already been processed." }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
