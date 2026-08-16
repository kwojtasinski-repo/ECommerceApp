using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Domain.Supporting.Verification;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeVerificationService
    {
        Task<BackofficeVerificationListVm> GetPendingAsync(VerificationPurpose? purpose = null, CancellationToken ct = default);
    }
}
