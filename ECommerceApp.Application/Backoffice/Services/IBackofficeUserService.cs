using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeUserService
    {
        Task<BackofficeUserListVm> GetUsersAsync(int pageSize, int pageNo, string? searchString, CancellationToken ct = default);
        Task<BackofficeUserDetailVm?> GetUserDetailAsync(string userId, CancellationToken ct = default);
    }
}
