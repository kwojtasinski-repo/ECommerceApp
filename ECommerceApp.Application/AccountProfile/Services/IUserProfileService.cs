using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.ViewModels;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Services
{
    public interface IUserProfileService
    {
        Task<int> CreateAsync(CreateUserProfileDto dto);
        Task<bool> UpdatePersonalInfoAsync(UpdateUserProfileDto dto);
        Task<bool> UpdateContactInfoAsync(UpdateContactInfoDto dto);
        Task<bool> DeleteAsync(int id);
        Task<UserProfileDetailsVm?> GetDetailsAsync(int id);
        Task<UserProfileDetailsVm?> GetDetailsByUserIdAsync(string userId);
        Task<UserProfileVm?> GetAsync(int id, string userId);
        Task<UserProfileVm?> GetByUserIdAsync(string userId);
        Task<UserProfileListVm> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<UserProfileListVm> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString);
        Task<bool> ExistsAsync(int id, string userId);
        Task<bool> AddAddressAsync(int userProfileId, string userId, AddAddressDto dto);
        Task<bool> UpdateAddressAsync(int userProfileId, string userId, UpdateAddressDto dto);
        Task<bool> RemoveAddressAsync(int userProfileId, int addressId, string userId);
    }
}
