using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Backoffice.Controllers
{
    [Area("Backoffice")]
    [Authorize(Roles = ManagingRole)]
    public class CurrenciesController : BaseController
    {
        private readonly IBackofficeCurrencyService _service;

        public CurrenciesController(IBackofficeCurrencyService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _service.GetCurrenciesAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _service.GetCurrencyDetailAsync(id);
            if (model is null)
                return NotFound();
            return View(model);
        }
    }
}
