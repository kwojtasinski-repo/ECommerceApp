using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Identity.IAM;

namespace ECommerceApp.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IOrderAccessService _orderAccessService;
        private readonly IVerificationCodeClient _verificationCodeClient;
        private readonly IOrderClient _orderClient;

        public LoginModel(SignInManager<ApplicationUser> signInManager, 
            ILogger<LoginModel> logger,
            IOrderAccessService orderAccessService,
            IVerificationCodeClient verificationCodeClient,
            IOrderClient orderClient)
        {
            _signInManager = signInManager;
            _logger = logger;
            _orderAccessService = orderAccessService;
            _verificationCodeClient = verificationCodeClient;
            _orderClient = orderClient;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        public string GuestOrderToken { get; private set; }

        // Deliberately NOT [BindProperty]: Razor Pages binds and validates every [BindProperty] on
        // every POST to this page regardless of which handler ran. GuestRecovery.Email is UI-only
        // (never read here — the recovery code always goes to the order's stored email, not the
        // visitor's typed value, per the anti-enumeration design) and its [Required] would
        // otherwise fail ModelState.IsValid for the unrelated plain-login POST whenever the
        // recovery form's field isn't present in the request.
        public GuestRecoveryInput GuestRecovery { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public class GuestRecoveryInput
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl = returnUrl ?? Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
            GuestOrderToken = await ResolveGuestOrderTokenAsync(Request.Query["guestOrder"]);
        }

        public async Task<IActionResult> OnPostRequestGuestRecoveryAsync(string guestOrderToken)
        {
            GuestOrderToken = guestOrderToken;
            await InitializePageAsync();
            var scope = await _orderAccessService.GetScopeAsync(
                GuestOrderToken,
                HttpContext.RequestAborted);
            if (scope is not null)
            {
                var email = await _orderClient.GetOrderCustomerEmailAsync(
                    scope.OrderId,
                    HttpContext.RequestAborted);
                if (!string.IsNullOrWhiteSpace(email))
                {
                    await _verificationCodeClient.RequestOrderAccessRecoveryAsync(
                        scope.OrderId,
                        HttpContext.RequestAborted);
                }
            }

            TempData["OrderRecoveryMessage"] = "Jeśli zamówienie jest powiązane z tym adresem, instrukcja odzyskania dostępu została przygotowana.";
            return RedirectToPage(new { guestOrder = GuestOrderToken });
        }

        private async Task InitializePageAsync()
        {
            ReturnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            GuestOrderToken = await ResolveGuestOrderTokenAsync(GuestOrderToken);
        }

        private async Task<string> ResolveGuestOrderTokenAsync(string token)
        {
            var scope = await _orderAccessService.GetScopeAsync(token, HttpContext.RequestAborted);
            return scope is null ? null : token;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
