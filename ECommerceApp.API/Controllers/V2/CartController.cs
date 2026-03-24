using ECommerceApp.API.Filters;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/cart")]
    public class CartController : BaseController
    {
        private readonly ICartService _cart;

        public CartController(ICartService cart)
        {
            _cart = cart;
        }

        [HttpGet]
        public async Task<IActionResult> GetCart(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var vm = await _cart.GetCartAsync(userId, ct);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost("items")]
        [Authorize(Policy = "TrustedApiUser")]
        [ServiceFilter(typeof(MaxApiQuantityFilter))]
        public async Task<IActionResult> AddOrUpdate([FromBody] AddToCartDto dto, CancellationToken ct = default)
        {
            await _cart.AddOrUpdateAsync(dto, ct);
            return Ok();
        }

        [HttpDelete("items/{productId:int}")]
        [Authorize(Policy = "TrustedApiUser")]
        public async Task<IActionResult> RemoveItem(int productId, CancellationToken ct = default)
        {
            var userId = GetUserId();
            await _cart.RemoveAsync(userId, productId, ct);
            return NoContent();
        }

        [HttpDelete]
        [Authorize(Policy = "TrustedApiUser")]
        public async Task<IActionResult> ClearCart(CancellationToken ct = default)
        {
            var userId = GetUserId();
            await _cart.ClearAsync(userId, ct);
            return NoContent();
        }
    }
}
