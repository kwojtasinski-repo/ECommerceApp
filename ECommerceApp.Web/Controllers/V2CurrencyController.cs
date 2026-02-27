using System.Threading.Tasks;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.Services;
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
        public IActionResult Add() => View();

        [HttpPost("add")]
        public async Task<IActionResult> Add(string code, string description)
        {
            await _currencyService.AddAsync(new CreateCurrencyDto(code, description));
            TempData["Success"] = "Currency added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _currencyService.GetByIdAsync(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpPost("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id, string code, string description)
        {
            await _currencyService.UpdateAsync(new UpdateCurrencyDto(id, code, description));
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
