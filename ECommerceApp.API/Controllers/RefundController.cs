using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/refunds")]
    [ApiController]
    public class RefundController : ControllerBase
    {
        private readonly IRefundService _refundService;

        public RefundController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
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
