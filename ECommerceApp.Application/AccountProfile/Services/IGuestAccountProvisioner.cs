using ECommerceApp.Application.AccountProfile.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Services
{
    public interface IGuestAccountProvisioner
    {
        Task<bool> IsRegisteredAsync(string userId);
        Task<IReadOnlySet<string>> GetRegisteredUserIdsAsync(IReadOnlyCollection<string> userIds);
        Task<GuestAccountProvisioningResult> CreateAsync(string email, string password);
        Task DeleteAsync(string userId);
    }
}