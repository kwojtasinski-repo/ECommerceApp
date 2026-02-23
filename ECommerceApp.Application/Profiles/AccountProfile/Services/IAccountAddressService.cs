using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.ViewModels;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    public interface IAccountAddressService
    {
        Task<int> AddAsync(AddAddressDto dto);
        Task<bool> UpdateAsync(UpdateAddressDto dto);
        Task<bool> DeleteAsync(int id, string userId);
        Task<AddressVm?> GetAsync(int id, string userId);
    }
}
