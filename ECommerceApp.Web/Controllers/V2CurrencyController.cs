using System.Threading.Tasks;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Application.Supporting.Currencies.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Route("v2/currencies")]
    public class V2CurrencyController : Controller
    {
        private readonly ICurrencyService _currencyService;

        public V2CurrencyController(ICurrencyService currencyService) =>
            _currencyService = currencyService;

        [HttpGet("")]
        public async Task<IActionResult> Index() =>
            View(await _currencyService.GetAllAsync());

        [HttpGet("details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var vm = await _currencyService.GetByIdAsync(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpGet("add")]
        public IActionResult Add() => View(new CreateCurrencyFormVm());

        [HttpPost("add")]
        public async Task<IActionResult> Add(CreateCurrencyFormVm vm)
        {
            if (!ModelState.IsValid) return View(vm);
            await _currencyService.AddAsync(new CreateCurrencyDto(vm.Code, vm.Description));
            TempData["Success"] = "Currency added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var currency = await _currencyService.GetByIdAsync(id);
            if (currency is null) return NotFound();
            return View(new UpdateCurrencyFormVm { Id = currency.Id, Code = currency.Code, Description = currency.Description });
        }

        [HttpPost("edit/{id:int}")]
        public async Task<IActionResult> Edit(UpdateCurrencyFormVm vm)
        {
            if (!ModelState.IsValid) return View(vm);
            await _currencyService.UpdateAsync(new UpdateCurrencyDto(vm.Id, vm.Code, vm.Description));
            TempData["Success"] = "Currency updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _currencyService.DeleteAsync(id);
            TempData["Success"] = "Currency deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
