using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Presale.Controllers
{
    [Area("Presale")]
    [Authorize]
    public class CheckoutController : BaseController
    {
        private readonly ICartService _cartService;
        private readonly ICheckoutService _checkoutService;

        public CheckoutController(ICartService cartService, ICheckoutService checkoutService)
        {
            _cartService = cartService;
            _checkoutService = checkoutService;
        }

        [HttpGet]
        public async Task<IActionResult> Cart()
        {
            var userId = new PresaleUserId(GetUserId());
            var cart = await _cartService.GetCartAsync(userId);
            return View(cart);
        }

        [HttpGet]
        public IActionResult AddItem(int id)
        {
            return View(model: id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(int productId, int quantity)
        {
            var userId = GetUserId();
            await _cartService.AddOrUpdateAsync(new AddToCartDto(userId, productId, quantity));
            return RedirectToAction(nameof(Cart));
        }

        [HttpGet]
        public async Task<IActionResult> PlaceOrder()
        {
            var userId = new PresaleUserId(GetUserId());
            var result = await _checkoutService.InitiateAsync(userId);
            if (result is InitiateCheckoutResult.CartEmpty || result is InitiateCheckoutResult.NothingReserved)
                return RedirectToAction(nameof(Cart));
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(
            int customerId, int currencyId,
            string firstName, string lastName, string email, string phoneNumber,
            bool isCompany, string? companyName, string? nip,
            string street, string buildingNumber, string? flatNumber,
            string zipCode, string city, string country)
        {
            var customer = new CheckoutCustomer(
                firstName, lastName, email, phoneNumber,
                isCompany, companyName, nip,
                street, buildingNumber, flatNumber,
                zipCode, city, country);
            var userId = new PresaleUserId(GetUserId());
            var result = await _checkoutService.PlaceOrderAsync(userId, customerId, currencyId, customer);
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

        [HttpPost]
        public async Task<IActionResult> UpdateCartItem(int productId, int quantity)
        {
            var userId = GetUserId();
            await _cartService.AddOrUpdateAsync(new AddToCartDto(userId, productId, quantity));
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
