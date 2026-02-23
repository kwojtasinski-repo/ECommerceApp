using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Profiles.AccountProfile
{
    public interface IContactDetailTypeRepository
    {
        Task<int> AddAsync(ContactDetailType contactDetailType);
        Task<ContactDetailType?> GetByIdAsync(int id);
        Task<List<ContactDetailType>> GetAllAsync();
        Task UpdateAsync(ContactDetailType contactDetailType);
        Task<bool> DeleteAsync(int id);
    }
}
