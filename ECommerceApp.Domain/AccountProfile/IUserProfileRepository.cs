using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.AccountProfile
{
    public interface IUserProfileRepository
    {
        Task<int> AddAsync(UserProfile profile);
        Task<UserProfile?> GetByIdAsync(int id);
        Task<UserProfile?> GetByIdAndUserIdAsync(int id, string userId);
        Task<UserProfile?> GetByUserIdAsync(string userId);
        Task UpdateAsync(UserProfile profile);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsByIdAsync(int id);
        Task<bool> ExistsByIdAndUserIdAsync(int id, string userId);
        Task<List<UserProfile>> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountAllAsync(string searchString);
        Task<List<UserProfile>> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString);
        Task<int> CountByUserIdAsync(string userId, string searchString);
        Task<List<UserProfile>> GetAllByUserIdAsync(string userId);
    }
}
