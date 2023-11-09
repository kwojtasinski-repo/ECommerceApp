using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface IUserManager<TUser> : IDisposable where TUser : class
    {
        Task<TUser> FindByNameAsync(string userName);
        Task<IList<string>> GetRolesAsync(TUser user);
    }
}
