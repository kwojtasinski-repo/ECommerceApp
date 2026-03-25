using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Application.Supporting.Currencies.ViewModels;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Currencies.Controllers
{
    [Area("Currencies")]
    [Authorize(Roles = MaintenanceRole)]
    public class CurrencyController : BaseController
    {
        private readonly ICurrencyService _currencyService;

        public CurrencyController(ICurrencyService currencyService)
        {
            _currencyService = currencyService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
            => View(await _currencyService.GetAllAsync(10, 1, ""));

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string searchString)
        {
            pageNo ??= 1;
            searchString ??= string.Empty;
            return View(await _currencyService.GetAllAsync(pageSize, pageNo.Value, searchString));
        }

        [HttpGet]
        public IActionResult Create()
            => View(new CreateCurrencyFormVm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCurrencyFormVm model)
        {
            if (!ModelState.IsValid)
                return View(model);
            try
            {
                await _currencyService.AddAsync(new CreateCurrencyDto(model.Code, model.Description));
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(nameof(Index), MapExceptionAsRouteValues(ex));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var currency = await _currencyService.GetByIdAsync(id);
            if (currency is null)
                return RedirectToAction(nameof(Index));
            return View(new UpdateCurrencyFormVm { Id = currency.Id, Code = currency.Code, Description = currency.Description });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateCurrencyFormVm model)
        {
            if (!ModelState.IsValid)
                return View(model);
            try
            {
                if (!await _currencyService.UpdateAsync(new UpdateCurrencyDto(model.Id, model.Code, model.Description)))
                    return RedirectToAction(nameof(Index));
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(nameof(Index), MapExceptionAsRouteValues(ex));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var currency = await _currencyService.GetByIdAsync(id);
            if (currency is null)
                return RedirectToAction(nameof(Index));
            return View(currency);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                return await _currencyService.DeleteAsync(id)
                    ? Json("deleted")
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }
    }
}
