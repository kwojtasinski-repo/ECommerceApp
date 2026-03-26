using ECommerceApp.Application.Identity.IAM.DTOs;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Identity.IAM.Services
{
    public interface IAuthenticationService
    {
        Task<SignInResponseDto> SignInAsync(SignInDto dto);
        Task<SignInResponseDto> RefreshAsync(string refreshToken);
        Task RevokeAsync(string refreshToken);
    }
}
