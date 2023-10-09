using System;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class RefundController : Controller
    {
        private readonly IRefundService _refundService;

        public RefundController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _refundService.GetRefunds(20, 1, "");
            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var refunds = _refundService.GetRefunds(pageSize, pageNo.Value, searchString);

            return View(refunds);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult EditRefund(RefundVm refund)
        {
            _refundService.UpdateRefund(refund);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        public IActionResult ViewRefundDetails(int id)
        {
            var refund = _refundService.GetRefundDetails(id);
            if(refund is null)
            {
                return NotFound();
            }
            return View(refund);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        public IActionResult DeleteRefund(int id)
        {
            _refundService.DeleteRefund(id);
            return RedirectToAction("Index");
        }
    }
}
