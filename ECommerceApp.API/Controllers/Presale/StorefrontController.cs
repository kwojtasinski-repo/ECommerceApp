using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.Presale
{
    [ApiController]
    [Route("api/storefront")]
    [AllowAnonymous]
    public sealed class StorefrontController : ControllerBase
    {
        private readonly IStorefrontQueryService _storefront;

        public StorefrontController(IStorefrontQueryService storefront)
        {
            _storefront = storefront;
        }

        // GET api/storefront/products?pageSize=10&pageNo=1&searchString=
        [HttpGet("products")]
        public async Task<ActionResult<StorefrontProductListVm>> GetProducts(
            [FromQuery] int pageSize = 10,
            [FromQuery] int pageNo = 1,
            [FromQuery] string searchString = "",
            CancellationToken ct = default)
        {
            var result = await _storefront.GetPublishedProductsAsync(pageSize, pageNo, searchString ?? "", ct);
            return Ok(result);
        }
    }
}
