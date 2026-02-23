using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.ViewModels;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    public interface IAccountProfileService
    {
        Task<int> CreateAsync(CreateAccountProfileDto dto);
        Task<bool> UpdateAsync(UpdateAccountProfileDto dto);
        Task<bool> DeleteAsync(int id);
        Task<AccountProfileDetailsVm?> GetDetailsAsync(int id);
        Task<AccountProfileDetailsVm?> GetDetailsByUserIdAsync(string userId);
        Task<AccountProfileVm?> GetAsync(int id, string userId);
        Task<AccountProfileVm?> GetByUserIdAsync(string userId);
        Task<AccountProfileListVm> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<AccountProfileListVm> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString);
        Task<bool> ExistsAsync(int id, string userId);
    }
}
