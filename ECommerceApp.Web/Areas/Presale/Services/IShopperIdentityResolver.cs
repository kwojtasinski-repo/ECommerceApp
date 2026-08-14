using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.AspNetCore.Http;

namespace ECommerceApp.Web.Areas.Presale.Services
{
    public interface IShopperIdentityResolver
    {
        PresaleUserId Resolve(HttpContext context);
    }
}