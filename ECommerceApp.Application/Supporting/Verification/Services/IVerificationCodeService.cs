using ECommerceApp.Domain.Supporting.Verification;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Verification.Services
{
    public interface IVerificationCodeService
    {
        Task<string> GenerateAsync(
            VerificationPurpose purpose,
            string subjectKey,
            TimeSpan validFor,
            CancellationToken ct = default);

        Task<string?> TryConsumeAsync(
            string code,
            VerificationPurpose purpose,
            CancellationToken ct = default);
    }
}