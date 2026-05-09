using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ECommerceApp.Web.Models;
using ECommerceApp.Application.Presale.Checkout.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IStorefrontQueryService _storefront;

        public HomeController(ILogger<HomeController> logger, IStorefrontQueryService storefront)
        {
            _logger = logger;
            _storefront = storefront;
        }

        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            var featured = await _storefront.GetPublishedProductsAsync(10, 1, string.Empty, null, ct);
            return View(featured);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
