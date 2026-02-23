using System.Threading.Tasks;

namespace ECommerceApp.Domain.Profiles.AccountProfile
{
    public interface IAddressRepository
    {
        Task<int> AddAsync(Address address);
        Task<Address?> GetByIdAsync(int id);
        Task<Address?> GetByIdAndUserIdAsync(int id, string userId);
        Task UpdateAsync(Address address);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsByIdAndUserIdAsync(int id, string userId);
    }
}
