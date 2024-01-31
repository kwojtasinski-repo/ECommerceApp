using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Refunds;
using ECommerceApp.Application.ViewModels.Refund;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class RefundController : BaseController
    {
        private readonly IRefundService _refundService;

        public RefundController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _refundService.GetRefunds(20, 1, "");
            return View(model);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var refunds = _refundService.GetRefunds(pageSize, pageNo.Value, searchString);
            return View(refunds);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult EditRefund(int id)
        {
            var refund = _refundService.Get(id);
            if(refund is null)
            {
                return NotFound();
            }
            return View(refund);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult EditRefund(RefundVm refund)
        {
            _refundService.UpdateRefund(refund);
            return RedirectToAction("Index");
        }

        public IActionResult ViewRefundDetails(int id)
        {
            var refund = _refundService.GetRefundDetails(id);
            if(refund is null)
            {
                return NotFound();
            }
            return View(refund);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeleteRefund(int id)
        {
            try
            {
                _refundService.DeleteRefund(id);
                return Json("deleted");
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult AddRefund(RefundVm refundVm)
        {
            try
            {
                _refundService.AddRefund(refundVm);
                return Json("Added");
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut]
        public IActionResult UpdateRefund(int id, RefundVm refundVm)
        {
            try
            {
                refundVm.Id = id;
                _refundService.UpdateRefund(refundVm);
                return Json("Updated");
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
