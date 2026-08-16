using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Supporting.Verification
{
    public interface IVerificationCodeRepository
    {
        Task AddAsync(VerificationCode verificationCode, CancellationToken ct = default);
        Task<VerificationCode> GetByCodeAsync(string code, VerificationPurpose purpose, CancellationToken ct = default);
        Task<IReadOnlyList<VerificationCode>> GetPendingAsync(CancellationToken ct = default);
        Task UpdateAsync(VerificationCode verificationCode, CancellationToken ct = default);
    }
}