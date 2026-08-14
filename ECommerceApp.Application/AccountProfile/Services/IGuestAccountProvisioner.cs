using ECommerceApp.Application.AccountProfile.Results;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Services
{
    public interface IGuestAccountProvisioner
    {
        Task<bool> IsRegisteredAsync(string userId);
        Task<GuestAccountProvisioningResult> CreateAsync(string email, string password);
        Task DeleteAsync(string userId);
    }
}