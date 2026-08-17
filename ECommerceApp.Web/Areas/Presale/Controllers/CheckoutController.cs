using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Options;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Application.AccountProfile.Results;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Web.Controllers;
using ECommerceApp.Web.Areas.Presale.Services;
using ECommerceApp.Web.Areas.Presale.Authorization;
using ECommerceApp.Web.Areas.Presale.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Presale.Controllers
{
    [Area("Presale")]
    [Authorize(Policy = "CustomerOrGuest")]
    public class CheckoutController : BaseController
    {
        private readonly ICartService _cartService;
        private readonly ICheckoutService _checkoutService;
        private readonly IAccountProfileClient _accountProfileClient;
        private readonly IShopperIdentityResolver _shopperIdentityResolver;
        private readonly IGuestPromotionService _guestPromotionService;
        private readonly IOrderAccessService _orderAccessService;
        private readonly IVerificationCodeClient _verificationCodeClient;
        private readonly IOrderClient _orderClient;
        private readonly IOrderAccessAuthorizer _orderAccessAuthorizer;

        public CheckoutController(
            ICartService cartService,
            ICheckoutService checkoutService,
            IAccountProfileClient accountProfileClient,
            IShopperIdentityResolver shopperIdentityResolver,
            IGuestPromotionService guestPromotionService,
            IOrderAccessService orderAccessService,
            IVerificationCodeClient verificationCodeClient,
            IOrderClient orderClient,
            IOrderAccessAuthorizer orderAccessAuthorizer)
        {
            _cartService = cartService;
            _checkoutService = checkoutService;
            _accountProfileClient = accountProfileClient;
            _shopperIdentityResolver = shopperIdentityResolver;
            _guestPromotionService = guestPromotionService;
            _orderAccessService = orderAccessService;
            _verificationCodeClient = verificationCodeClient;
            _orderClient = orderClient;
            _orderAccessAuthorizer = orderAccessAuthorizer;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Cart()
        {
            var requestStartedAt = DateTime.UtcNow;
            var userId = _shopperIdentityResolver.Resolve(HttpContext);
            var cart = await _cartService.GetCartAsync(userId);
            var secondsRemaining = await _checkoutService.GetSecondsRemainingAsync(userId, requestStartedAt);
            var hasActive = secondsRemaining.HasValue;
            var vm = cart is not null
                ? cart with { HasActiveCheckout = hasActive, SecondsRemaining = secondsRemaining }
                : new CartVm(userId.Value, System.Array.Empty<CartLineVm>(), hasActive, secondsRemaining);
            return View(vm);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> PlaceOrder()
        {
            var userId = _shopperIdentityResolver.Resolve(HttpContext);
            var result = await _checkoutService.InitiateAsync(userId);

            return result switch
            {
                InitiateCheckoutResult.CartEmpty => RedirectToAction(nameof(Cart)),
                InitiateCheckoutResult.NothingReserved => RedirectToAction(nameof(Cart)),
                InitiateCheckoutResult.AlreadyInProgress => RedirectToAction(nameof(Cart)),
                InitiateCheckoutResult.Completed => View(new PlaceOrderVm()),
                _ => RedirectToAction(nameof(Cart))
            };
        }

        [HttpGet]
        public async Task<IActionResult> ResumeOrder()
        {
            var userId = new PresaleUserId(GetUserId());
            var hasActive = await _checkoutService.HasActiveCheckoutAsync(userId);
            if (!hasActive)
                return RedirectToAction(nameof(Cart));
            return View(nameof(PlaceOrder), new PlaceOrderVm());
        }

        [HttpGet]
        public async Task<IActionResult> GetProfileForCheckout()
        {
            var profile = await _accountProfileClient.GetProfileAsync(GetUserId());
            return Json(profile);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> CheckoutStatus()
        {
            var requestStartedAt = DateTime.UtcNow;
            var userId = _shopperIdentityResolver.Resolve(HttpContext);
            var secondsRemaining = await _checkoutService.GetSecondsRemainingAsync(userId, requestStartedAt);
            if (secondsRemaining is null)
                return Json(new { active = false, secondsRemaining = (int?)null });
            return Json(new { active = true, secondsRemaining = secondsRemaining.Value });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("GuestCheckoutPlaceOrder")]
        public async Task<IActionResult> PlaceOrder(PlaceOrderVm vm)
        {
            var customer = new CheckoutCustomer(
                vm.FirstName, vm.LastName, vm.Email, vm.PhoneNumber,
                vm.IsCompany, vm.CompanyName, vm.Nip,
                vm.Street, vm.BuildingNumber, vm.FlatNumber,
                vm.ZipCode, vm.City, vm.Country);
                var userId = _shopperIdentityResolver.Resolve(HttpContext);
                int customerId;
                // A GuestAccess sign-in (Phase 9) also sets IsAuthenticated = true, so that alone can no
                // longer distinguish a real registered account (which selects an existing CustomerId)
                // from a guest (who supplies customer details inline, even on a repeat order placed
                // after their first GuestAccess sign-in).
                if (User.Identity?.AuthenticationType == IdentityConstants.ApplicationScheme)
                {
                    if (!vm.CustomerId.HasValue)
                    {
                        ModelState.AddModelError(nameof(vm.CustomerId), "CustomerId is required.");
                        return View(vm);
                    }

                    customerId = vm.CustomerId.Value;
                }
                else
                {
                    customerId = await _accountProfileClient.EnsureGuestCustomerAsync(userId.Value, customer);
                }

                var result = await _checkoutService.PlaceOrderAsync(userId, customerId, vm.CurrencyId, customer);
            return result switch
            {
                CheckoutResult.Success s => await CompleteOrderAccessAsync(s.OrderId, customerId),
                CheckoutResult.NoSoftReservations => RedirectToAction(nameof(Cart)),
                CheckoutResult.ReservationsExpired => RedirectToAction(nameof(Cart)),
                _ => View(vm)
            };
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelCheckout()
        {
            var userId = _shopperIdentityResolver.Resolve(HttpContext);
            await _checkoutService.CancelAsync(userId);
            return RedirectToAction(nameof(Cart));
        }

        [HttpGet]
        public IActionResult OrderDetails()
        {
            return RedirectToAction(nameof(PlaceOrder));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Summary(int id, int? profileId, bool guest = false)
        {
            var order = await _orderClient.GetOrderAccessInfoAsync(id, HttpContext.RequestAborted);
            if (order is null)
                return NotFound();
            if (!await IsOrderAuthorizedAsync(order))
                return OrderAccessDenial.Result(this, User, id);

            ViewBag.GuestProfileId = guest ? profileId : null;
            return View(model: id);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Order(int id, string message = null)
        {
            var order = await _orderClient.GetOrderAccessInfoAsync(id, HttpContext.RequestAborted);
            if (order is null)
                return NotFound();
            if (await IsOrderAuthorizedAsync(order))
                return RedirectToAction(nameof(Summary), new { id });
            if (!order.UserId.StartsWith("gst_", StringComparison.OrdinalIgnoreCase))
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl = Url.Action(nameof(Order), new { id }) });

            return View(new OrderAccessLookupVm { OrderId = id, Message = message });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("OrderAccessByOrderId")]
        public async Task<IActionResult> RequestOrderAccess(int id, string email)
        {
            var order = await _orderClient.GetOrderAccessInfoAsync(id, HttpContext.RequestAborted);
            if (order is not null && order.UserId.StartsWith("gst_", StringComparison.OrdinalIgnoreCase))
            {
                await _verificationCodeClient.RequestOrderAccessRecoveryAsync(
                    id,
                    HttpContext.RequestAborted);
            }

            return RedirectToAction(nameof(Order), new
            {
                id,
                message = "Jeśli zamówienie jest powiązane z tym adresem, kod dostępu został przygotowany."
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("OrderAccessByOrderId")]
        public async Task<IActionResult> ConfirmOrderAccess(int id, string code)
        {
            var result = await _verificationCodeClient.RedeemOrderAccessRecoveryAsync(
                code,
                HttpContext.RequestAborted);
            if (!result.Success || result.OrderId != id)
                return RedirectToAction(nameof(Order), new { id, message = "Kod dostępu jest nieprawidłowy lub wygasł." });

            await CreateGuestAccessAsync(result.OrderId, result.UserProfileId);
            return RedirectToAction(nameof(Summary), new { id = result.OrderId });
        }

        private async Task<IActionResult> CompleteOrderAccessAsync(int orderId, int userProfileId)
        {
            var token = OrderAccessTokenGenerator.NewToken();
            await _orderAccessService.CreateAsync(
                orderId,
                userProfileId,
                token,
                HttpContext.RequestAborted);
            // A caller who isn't signed in as a real registered user is a guest — whether this is their
            // first order (fully anonymous) or a repeat order placed while already GuestAccess-signed-in
            // for a *previous* order. Either way, (re-)mint the sign-in scoped to *this* order: the
            // GuestAccess ticket is single-order by design, so a second order must replace the old scope,
            // not leave the guest holding a stale ticket that only ever unlocks their first order.
            var isGuest = User.Identity?.AuthenticationType != IdentityConstants.ApplicationScheme;
            if (isGuest)
            {
                await SignInGuestAccessAsync(orderId, _shopperIdentityResolver.Resolve(HttpContext).Value, token);
            }
            return RedirectToAction(nameof(Summary), new
            {
                id = orderId,
                profileId = userProfileId,
                guest = isGuest
            });
        }

        private async Task CreateGuestAccessAsync(int orderId, int userProfileId)
        {
            var token = OrderAccessTokenGenerator.NewToken();
            await _orderAccessService.CreateAsync(
                orderId,
                userProfileId,
                token,
                HttpContext.RequestAborted);
            await SignInGuestAccessAsync(orderId, await ResolveGuestUserIdAsync(orderId), token);
        }

        private async Task SignInGuestAccessAsync(int orderId, string userId, string backingToken)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(GuestAccessDefaults.OrderIdClaim, orderId.ToString()),
                    new Claim(GuestAccessDefaults.BackingTokenClaim, backingToken),
                    new Claim(GuestAccessDefaults.ValidatedAtClaim,
                        DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture))
                },
                GuestAccessDefaults.AuthenticationScheme));
            await HttpContext.SignInAsync(
                GuestAccessDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                });
        }

        private async Task<string> ResolveGuestUserIdAsync(int orderId)
        {
            var order = await _orderClient.GetOrderAccessInfoAsync(orderId, HttpContext.RequestAborted);
            return order?.UserId ?? string.Empty;
        }

        private Task<bool> IsOrderAuthorizedAsync(OrderAccessInfo order)
        {
            return _orderAccessAuthorizer.AuthorizeAsync(
                User,
                new OrderAccessResource(order.OrderId, order.UserId),
                HttpContext.RequestAborted);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount(int orderId, int profileId, string password)
        {
            var requestingUserId = _shopperIdentityResolver.Resolve(HttpContext).Value;
            var result = await _guestPromotionService.PromoteAsync(profileId, requestingUserId, password);
            return result.Status switch
            {
                PromotionStatus.Success => RedirectToAction(nameof(Summary), new { id = orderId }),
                PromotionStatus.ProfileNotFound => NotFound(),
                PromotionStatus.NotOwner => Forbid(),
                PromotionStatus.AlreadyRegistered => Conflict(),
                PromotionStatus.IdentityCreationFailed => BadRequest(result.Errors),
                _ => BadRequest()
            };
        }

        [HttpGet]
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
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("GuestCheckoutAddToCart")]
        public async Task<IActionResult> AddToCart(int productId, int quantity, string returnUrl)
        {
            if (quantity < 1)
            {
                return BadRequest();
            }

            var userId = _shopperIdentityResolver.Resolve(HttpContext);
            var result = await _cartService.AddToCartAsync(new AddToCartDto(userId.Value, productId, quantity));
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
