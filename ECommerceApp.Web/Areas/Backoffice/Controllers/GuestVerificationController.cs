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
                item.RedemptionUrl = Url.Page(
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
