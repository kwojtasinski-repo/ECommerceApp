using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface ISignInManager<TUser> where TUser : class
    {
        Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure);
    }
}
