using ECommerceApp.Domain.Shared;
using ECommerceApp.Domain.Supporting.Verification;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Verification.Services
{
    internal sealed class VerificationCodeService : IVerificationCodeService
    {
        private readonly IVerificationCodeRepository _repository;

        public VerificationCodeService(IVerificationCodeRepository repository)
        {
            _repository = repository;
        }

        public async Task<string> GenerateAsync(
            VerificationPurpose purpose,
            string subjectKey,
            TimeSpan validFor,
            CancellationToken ct = default)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var code = Convert.ToHexString(bytes).ToLowerInvariant();
            var verificationCode = VerificationCode.Create(
                purpose,
                subjectKey,
                code,
                DateTime.UtcNow.Add(validFor));

            await _repository.AddAsync(verificationCode, ct);
            return code;
        }

        public async Task<bool> TryConsumeAsync(
            string code,
            VerificationPurpose purpose,
            CancellationToken ct = default)
        {
            var verificationCode = await _repository.GetByCodeAsync(code, purpose, ct);
            if (verificationCode == null)
            {
                return false;
            }

            try
            {
                verificationCode.Consume();
            }
            catch (DomainException)
            {
                return false;
            }

            await _repository.UpdateAsync(verificationCode, ct);
            return true;
        }
    }
}