using ECommerceApp.Application.AccountProfile.Contracts;
using ECommerceApp.Domain.Identity.IAM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LinkGuestOrdersModel : PageModel
    {
        private readonly IVerificationCodeClient _verificationCodeClient;
        private readonly UserManager<ApplicationUser> _userManager;

        public LinkGuestOrdersModel(
            IVerificationCodeClient verificationCodeClient,
            UserManager<ApplicationUser> userManager)
        {
            _verificationCodeClient = verificationCodeClient;
            _userManager = userManager;
        }

        public bool IsSuccess { get; private set; }
        public int ProfilesLinked { get; private set; }

        public async Task<IActionResult> OnGetAsync(string code)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                var returnUrl = Url.Page(
                    "/Account/LinkGuestOrders",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(userId))
            {
                return Page();
            }

            var result = await _verificationCodeClient.RedeemGuestAccountLinkAsync(
                code,
                userId,
                HttpContext.RequestAborted);
            IsSuccess = result.Success;
            ProfilesLinked = result.ProfilesLinked;
            return Page();
        }
    }
}
