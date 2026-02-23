using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    public interface IAccountContactDetailTypeService
    {
        Task<int> AddAsync(AddContactDetailTypeDto dto);
        Task<bool> UpdateAsync(UpdateContactDetailTypeDto dto);
        Task<bool> DeleteAsync(int id);
        Task<ContactDetailTypeVm?> GetAsync(int id);
        Task<List<ContactDetailTypeVm>> GetAllAsync();
    }
}
