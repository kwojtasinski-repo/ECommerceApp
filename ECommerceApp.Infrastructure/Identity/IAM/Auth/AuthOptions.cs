using ECommerceApp.Application.Identity.IAM.Services;

namespace ECommerceApp.Infrastructure.Identity.IAM.Auth
{
    internal sealed class AuthOptions : IRefreshTokenOptions
    {
        public string Key { get; set; }
        public string Issuer { get; set; }
        public int RefreshTokenTtlDays { get; set; } = 7;
    }
}
