﻿using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.ViewModels.Currency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{MaintenanceRole}")]
    public class CurrencyController : BaseController
    {
        private readonly ICurrencyService _currencyService;
        
        public CurrencyController(ICurrencyService currencyService)
        {
            _currencyService = currencyService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = _currencyService.GetAllCurrencies(10, 1, "");

            return View(model);
        }

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

            var model = _currencyService.GetAllCurrencies(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [HttpGet]
        public IActionResult AddCurrency()
        {
            var currency = new CurrencyVm { Currency = new () };
            return View(currency);
        }

        [HttpPost]
        public IActionResult AddCurrency(CurrencyVm model)
        {
            try
            {
                _currencyService.Add(model.Currency);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult EditCurrency(int id)
        {
            var currency = _currencyService.GetById(id);
            if (currency is null)
            {
                var errorModel = BuildErrorModel("currencyNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new CurrencyVm());
            }
            return View(new CurrencyVm { Currency = currency });
        }

        [HttpPost]
        public IActionResult EditCurrency(CurrencyVm model)
        {
            try
            {
                _currencyService.Update(model.Currency);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult ViewCurrency(int id)
        {
            var currency = _currencyService.GetById(id);
            if (currency is null)
            {
                var errorModel = BuildErrorModel("currencyNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new CurrencyDto());
            }
            return View(new CurrencyVm { Currency = currency });
        }

        public IActionResult DeleteCurrency(int id)
        {
            try
            {
                _currencyService.Delete(id);
                return Json("deleted");
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
