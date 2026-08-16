using ECommerceApp.Application.AccountProfile.Contracts;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Supporting.Verification.Services;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.Supporting.Verification;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.AccountProfile.Adapters
{
    internal sealed class VerificationCodeClientAdapter : IVerificationCodeClient
    {
        private readonly IVerificationCodeService _verificationCodeService;
        private readonly IUserProfileRepository _profileRepository;
        private readonly IGuestAccountProvisioner _accountProvisioner;

        public VerificationCodeClientAdapter(
            IVerificationCodeService verificationCodeService,
            IUserProfileRepository profileRepository,
            IGuestAccountProvisioner accountProvisioner)
        {
            _verificationCodeService = verificationCodeService;
            _profileRepository = profileRepository;
            _accountProvisioner = accountProvisioner;
        }

        public Task<string> RequestGuestAccountLinkAsync(string email, CancellationToken ct = default)
            => _verificationCodeService.GenerateAsync(
                VerificationPurpose.GuestAccountLink,
                email,
                TimeSpan.FromDays(7),
                ct);

        public async Task<GuestLinkRedemptionResult> RedeemGuestAccountLinkAsync(
            string code,
            string newUserId,
            CancellationToken ct = default)
        {
            var email = await _verificationCodeService.TryConsumeAsync(
                code,
                VerificationPurpose.GuestAccountLink,
                ct);
            if (email is null)
            {
                return new GuestLinkRedemptionResult(false, 0);
            }

            var candidates = await _profileRepository.GetByEmailAsync(email, track: true);
            var registeredUserIds = await _accountProvisioner.GetRegisteredUserIdsAsync(
                candidates.Select(profile => profile.UserId).ToList());
            var unclaimed = candidates
                .Where(profile => !registeredUserIds.Contains(profile.UserId))
                .ToList();

            foreach (var profile in unclaimed)
            {
                profile.ReassignOwner(newUserId);
                await _profileRepository.UpdateAsync(profile);
            }

            return new GuestLinkRedemptionResult(true, unclaimed.Count);
        }
    }
}
