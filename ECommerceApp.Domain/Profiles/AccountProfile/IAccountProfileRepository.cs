using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Profiles.AccountProfile
{
    public interface IAccountProfileRepository
    {
        Task<int> AddAsync(AccountProfile profile);
        Task<AccountProfile?> GetByIdAsync(int id);
        Task<AccountProfile?> GetByIdWithDetailsAsync(int id);
        Task<AccountProfile?> GetByIdAndUserIdAsync(int id, string userId);
        Task<AccountProfile?> GetByUserIdAsync(string userId);
        Task UpdateAsync(AccountProfile profile);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsByIdAsync(int id);
        Task<bool> ExistsByIdAndUserIdAsync(int id, string userId);
        Task<List<AccountProfile>> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountAllAsync(string searchString);
        Task<List<AccountProfile>> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString);
        Task<int> CountByUserIdAsync(string userId, string searchString);
        Task<List<AccountProfile>> GetAllByUserIdAsync(string userId);
    }
}
