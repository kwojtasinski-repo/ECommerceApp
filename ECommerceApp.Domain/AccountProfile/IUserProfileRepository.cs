using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.AccountProfile
{
    public interface IUserProfileRepository
    {
        Task<UserProfileId> AddAsync(UserProfile profile);
        Task<UserProfile?> GetByIdAsync(UserProfileId id);
        Task<UserProfile?> GetByIdAndUserIdAsync(UserProfileId id, string userId);
        Task<UserProfile?> GetByUserIdAsync(string userId);
        Task UpdateAsync(UserProfile profile);
        Task<bool> DeleteAsync(UserProfileId id);
        Task<bool> ExistsByIdAsync(UserProfileId id);
        Task<bool> ExistsByIdAndUserIdAsync(UserProfileId id, string userId);
        Task<List<UserProfile>> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountAllAsync(string searchString);
        Task<List<UserProfile>> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString);
        Task<int> CountByUserIdAsync(string userId, string searchString);
        Task<List<UserProfile>> GetAllByUserIdAsync(string userId);
    }
}
