using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Permissions;
using ECommerceApp.Domain.Supporting.Verification;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Backoffice.Controllers
{
    [Area("Backoffice")]
    [Authorize(Roles = UserPermissions.Roles.Administrator)]
    public class GuestVerificationController : BaseController
    {
        private readonly IBackofficeVerificationService _service;

        public GuestVerificationController(IBackofficeVerificationService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index(VerificationPurpose? purpose = null)
        {
            var model = await _service.GetPendingAsync(purpose, HttpContext.RequestAborted);
            foreach (var item in model.Codes)
            {
                // ADR-0030 Phase 9: order-access recovery is self-service (email + code, entered by hand
                // on the unified lookup page) — there is no more magic-link action to redeem it via GET.
                // CheckoutController.RedeemRecovery no longer exists (repurposed into ConfirmOrderAccess,
                // a POST). Link to the lookup page itself instead; the code column is shown separately
                // (see Index.cshtml) for the admin/tester to copy into it by hand.
                item.RedemptionUrl = item.Purpose == VerificationPurpose.GuestOrderAccess
                    ? Url.Action(
                        "Order",
                        "Checkout",
                        new { area = "Presale", id = item.SubjectKey },
                        Request.Scheme,
                        Request.Host.Value)
                    : Url.Page(
                        "/Account/LinkGuestOrders",
                        pageHandler: null,
                        values: new { area = "Identity", code = item.Code },
                        protocol: Request.Scheme,
                        host: Request.Host.Value);
            }

            return View(model);
        }
    }
}
