﻿using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Order;
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
        private readonly OrderServiceAbstract _orderService;

        public RefundController(OrderServiceAbstract orderService)
        {
            _orderService = orderService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("all")]
        public ActionResult<List<RefundForListVm>> GetRefunds()
        {
            var refunds = _orderService.GetAllRefunds();
            if (refunds.Count == 0)
            {
                return NotFound();
            }
            return Ok(refunds);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("{id}")]
        public ActionResult<RefundDetailsVm> GetRefund(int id)
        {
            var refund = _orderService.GetRefundDetail(id);
            if (refund == null)
            {
                return NotFound();
            }
            return Ok(refund);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditRefund([FromBody] CreateRefundVm model)
        {
            var modelExists = _orderService.CheckIfRefundExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            var refund = model.MapToNewRefund();
            _orderService.UpdateRefund(refund);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public ActionResult<int> AddRefund([FromBody] CreateRefundVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var refund = model.MapToNewRefund();
            var id = _orderService.AddRefund(refund);
            return Ok(id);
        }
    }
}