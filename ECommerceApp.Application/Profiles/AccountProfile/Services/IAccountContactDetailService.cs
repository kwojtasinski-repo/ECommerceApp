using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    public interface IAccountContactDetailService
    {
        Task<int> AddAsync(AddContactDetailDto dto);
        Task<bool> UpdateAsync(UpdateContactDetailDto dto);
        Task<bool> DeleteAsync(int id, string userId);
        Task<ContactDetailVm?> GetAsync(int id, string userId);
        Task<List<ContactDetailVm>> GetAllAsync();
    }
}
