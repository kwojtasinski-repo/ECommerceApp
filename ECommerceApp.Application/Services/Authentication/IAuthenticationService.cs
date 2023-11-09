using ECommerceApp.Application.DTO;

namespace ECommerceApp.Application.Services.Authentication
{
    public interface IAuthenticationService
    {
        SignInResponseDto SignIn(SignInDto signInDto);
    }
}
