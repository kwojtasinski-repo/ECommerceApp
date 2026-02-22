namespace ECommerceApp.Application.Identity.IAM.DTOs
{
    public record SignInResponseDto(string AccessToken, string RefreshToken);
}
