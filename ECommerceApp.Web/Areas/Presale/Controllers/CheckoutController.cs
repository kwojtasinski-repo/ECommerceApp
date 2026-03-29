using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Options;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Presale.Controllers
{
    [Area("Presale")]
    [Authorize]
    public class CheckoutController : BaseController
    {
        private readonly ICartService _cartService;
        private readonly ICheckoutService _checkoutService;
        private readonly IAccountProfileClient _accountProfileClient;

        public CheckoutController(ICartService cartService, ICheckoutService checkoutService, IAccountProfileClient accountProfileClient)
        {
            _cartService = cartService;
            _checkoutService = checkoutService;
            _accountProfileClient = accountProfileClient;
        }

        [HttpGet]
        public async Task<IActionResult> Cart()
        {
            var userId = new PresaleUserId(GetUserId());
            var cart = await _cartService.GetCartAsync(userId);
            return View(cart);
        }

        [HttpGet]
        public async Task<IActionResult> PlaceOrder()
        {
            var userId = new PresaleUserId(GetUserId());
            var result = await _checkoutService.InitiateAsync(userId);
            if (result is InitiateCheckoutResult.CartEmpty || result is InitiateCheckoutResult.NothingReserved)
            {
                return RedirectToAction(nameof(Cart));
            }

            return View(new PlaceOrderVm());
        }

        [HttpGet]
        public async Task<IActionResult> GetProfileForCheckout()
        {
            var profile = await _accountProfileClient.GetProfileAsync(GetUserId());
            return Json(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(PlaceOrderVm vm)
        {
            var customer = new CheckoutCustomer(
                vm.FirstName, vm.LastName, vm.Email, vm.PhoneNumber,
                vm.IsCompany, vm.CompanyName, vm.Nip,
                vm.Street, vm.BuildingNumber, vm.FlatNumber,
                vm.ZipCode, vm.City, vm.Country);
            var userId = new PresaleUserId(GetUserId());
            var result = await _checkoutService.PlaceOrderAsync(userId, vm.CustomerId, vm.CurrencyId, customer);
            return result switch
            {
                CheckoutResult.Success s => RedirectToAction(nameof(Summary), new { id = s.OrderId }),
                _ => RedirectToAction(nameof(Cart))
            };
        }

        [HttpGet]
        public IActionResult OrderDetails()
        {
            return RedirectToAction(nameof(PlaceOrder));
        }

        [HttpGet]
        public IActionResult Summary(int id)
        {
            return View(model: id);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> CartCount()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { count = 0 });
            }

            var userId = new PresaleUserId(GetUserId());
            var cart = await _cartService.GetCartAsync(userId);
            return Json(new { count = cart?.Lines.Sum(l => l.Quantity) ?? 0 });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity, string returnUrl)
        {
            if (!User.Identity.IsAuthenticated)
            {
                var safeReturn = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? returnUrl : Url.Action(nameof(Cart));
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl = safeReturn });
            }
            if (quantity < 1)
            {
                return BadRequest();
            }

            var userId = GetUserId();
            var result = await _cartService.AddToCartAsync(new AddToCartDto(userId, productId, quantity));
            return result switch
            {
                AddToCartResult.Success => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? Redirect(returnUrl) : RedirectToAction(nameof(Cart)),
                _ => BadRequest()
            };
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCartItem(int productId, int quantity)
        {
            if (quantity < 1 || quantity > CheckoutOptions.MaxWebQuantityPerOrderLine)
            {
                return BadRequest();
            }

            var userId = GetUserId();
            await _cartService.SetCartItemAsync(new AddToCartDto(userId, productId, quantity));
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCartItem(int id)
        {
            var userId = GetUserId();
            await _cartService.RemoveAsync(userId, id);
            return Ok();
        }
    }
}
