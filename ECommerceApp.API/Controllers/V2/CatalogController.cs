using ECommerceApp.Application.Catalog.Products.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/items")]
    public class CatalogController : BaseController
    {
        private readonly IProductService _products;

        public CatalogController(IProductService products)
        {
            _products = products;
        }

        [HttpGet]
        public async Task<IActionResult> GetItems(
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNo = 1,
            [FromQuery] string searchString = "",
            CancellationToken ct = default)
        {
            var vm = await _products.GetPublishedProducts(pageSize, pageNo, searchString ?? "");
            return Ok(vm);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetItem(int id, CancellationToken ct = default)
        {
            var vm = await _products.GetProductDetails(id, ct);
            return vm is null ? NotFound() : Ok(vm);
        }
    }
}
