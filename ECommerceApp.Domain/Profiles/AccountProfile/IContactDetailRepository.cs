using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Profiles.AccountProfile
{
    public interface IContactDetailRepository
    {
        Task<int> AddAsync(ContactDetail contactDetail);
        Task<ContactDetail?> GetByIdAsync(int id);
        Task<ContactDetail?> GetByIdAndUserIdAsync(int id, string userId);
        Task<List<ContactDetail>> GetAllAsync();
        Task UpdateAsync(ContactDetail contactDetail);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsByIdAndUserIdAsync(int id, string userId);
    }
}
