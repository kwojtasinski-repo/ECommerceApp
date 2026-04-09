using ECommerceApp.Application.Backoffice.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeUserService : IBackofficeUserService
    {
        public Task<BackofficeUserListVm> GetUsersAsync(int pageSize, int pageNo, string? searchString, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeUserDetailVm?> GetUserDetailAsync(string userId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
