namespace ECommerceApp.Application.Identity.IAM.Services
{
    public interface IRefreshTokenOptions
    {
        int RefreshTokenTtlDays { get; }
    }
}
