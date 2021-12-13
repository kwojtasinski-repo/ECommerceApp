﻿using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Currency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = "Administrator, Admin, Manager, Service")]
    public class CurrencyController : Controller
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
            var currency = new CurrencyVm();
            return View(currency);
        }

        [HttpPost]
        public IActionResult AddCurrency(CurrencyVm model)
        {
            var id = _currencyService.Add(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditCurrency(int id)
        {
            var currency = _currencyService.Get(id);
            return View(currency);
        }

        [HttpPost]
        public IActionResult EditCurrency(CurrencyVm model)
        {
            _currencyService.Update(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult ViewCurrency(int id)
        {
            var currency = _currencyService.Get(id);
            return View(currency);
        }

        public IActionResult DeleteCurrency(int id)
        {
            _currencyService.Delete(id);
            return RedirectToAction("Index");
        }
    }
}
