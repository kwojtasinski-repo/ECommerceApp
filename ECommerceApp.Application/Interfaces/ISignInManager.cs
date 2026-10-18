using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface ISignInManager<TUser> where TUser : class
    {
        Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure);

        /// <summary>
        /// Validates the password (including the confirmed-account check) without signing the user in, so no
        /// authentication cookie is issued. For token-based callers.
        /// </summary>
        Task<SignInResult> CheckPasswordSignInAsync(TUser user, string password, bool lockoutOnFailure);
    }
}
