using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Domain.Supporting.Verification;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeVerificationService : IBackofficeVerificationService
    {
        private readonly IVerificationCodeRepository _repository;

        public BackofficeVerificationService(IVerificationCodeRepository repository)
        {
            _repository = repository;
        }

        public async Task<BackofficeVerificationListVm> GetPendingAsync(
            VerificationPurpose? purpose = null,
            CancellationToken ct = default)
        {
            var codes = await _repository.GetPendingAsync(ct);
            return new BackofficeVerificationListVm
            {
                PurposeFilter = purpose,
                Codes = codes
                    .Where(code => !purpose.HasValue || code.Purpose == purpose.Value)
                    .OrderBy(code => code.ExpiresAt)
                    .Select(code => new BackofficeVerificationItemVm
                    {
                        Purpose = code.Purpose,
                        SubjectKey = code.SubjectKey,
                        Code = code.Code,
                        ExpiresAt = code.ExpiresAt
                    })
                    .ToList()
            };
        }
    }
}
