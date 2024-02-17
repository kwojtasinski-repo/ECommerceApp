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
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("refundNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new RefundVm());
            }
            return View(refund);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult EditRefund(RefundVm refund)
        {
            try
            {
                if (!_refundService.UpdateRefund(refund))
                {
                    var errorModel = BuildErrorModel(ErrorCode.Create("refundNotFound", ErrorParameter.Create("id", refund.Id)));
                    return RedirectToAction("Index", errorModel.AsOjectRoute());
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        public IActionResult ViewRefundDetails(int id)
        {
            var refund = _refundService.GetRefundDetails(id);
            if(refund is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("refundNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new RefundDetailsVm());
            }
            return View(refund);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeleteRefund(int id)
        {
            try
            {
                return _refundService.DeleteRefund(id)
                        ? Json("deleted")
                        : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
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
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut]
        public IActionResult UpdateRefund(int id, RefundVm refundVm)
        {
            try
            {
                refundVm.Id = id;
                return _refundService.UpdateRefund(refundVm)
                    ? Json("Updated")
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }
    }
}
