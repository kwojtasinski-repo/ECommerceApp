using ECommerceApp.Application.Services.Refunds;
using ECommerceApp.Application.ViewModels.Refund;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/refunds")]
    public class RefundController : BaseController
    {
        private readonly IRefundService _refundService;

        public RefundController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public ActionResult<ListForRefundVm> GetRefunds([FromQuery] int pageSize = 20, int pageNo = 1, string searchString = "")
        {
            var refunds = _refundService.GetRefunds(pageSize, pageNo, searchString);
            if (refunds.Refunds.Count == 0)
            {
                return NotFound();
            }
            return Ok(refunds);
        }

        [HttpGet("{id}")]
        public ActionResult<RefundDetailsVm> GetRefund(int id)
        {
            var refund = _refundService.GetRefundDetails(id);
            if (refund == null)
            {
                return NotFound();
            }
            return Ok(refund);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut]
        public IActionResult EditRefund([FromBody] CreateRefundVm model)
        {
            var modelExists = _refundService.RefundExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            var refund = model.MapToNewRefund();
            _refundService.UpdateRefund(refund);
            return Ok();
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public ActionResult<int> AddRefund([FromBody] CreateRefundVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var refund = model.MapToNewRefund();
            var id = _refundService.AddRefund(refund);
            return Ok(id);
        }
    }
}
