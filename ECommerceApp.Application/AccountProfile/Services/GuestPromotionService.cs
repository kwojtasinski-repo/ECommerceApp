using ECommerceApp.Application.AccountProfile.Results;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.AccountProfile;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Services
{
    internal sealed class GuestPromotionService : IGuestPromotionService
    {
        private readonly IUserProfileRepository _profileRepository;
        private readonly IGuestAccountProvisioner _accountProvisioner;

        public GuestPromotionService(IUserProfileRepository profileRepository, IGuestAccountProvisioner accountProvisioner)
        {
            _profileRepository = profileRepository;
            _accountProvisioner = accountProvisioner;
        }

        public async Task<PromotionResult> PromoteAsync(int profileId, string requestingUserId, string password, CancellationToken ct = default)
        {
            var profile = await _profileRepository.GetByIdAsync(new UserProfileId(profileId), track: true);
            if (profile is null)
                return PromotionResult.ProfileNotFound();

            if (profile.UserId != requestingUserId)
                return PromotionResult.NotOwner();

            if (await _accountProvisioner.IsRegisteredAsync(profile.UserId))
                return PromotionResult.AlreadyRegistered();

            var createResult = await _accountProvisioner.CreateAsync(profile.Email.Value, password);
            if (!createResult.Succeeded)
                return PromotionResult.IdentityCreationFailed(createResult.Errors);

            try
            {
                profile.ReassignOwner(createResult.UserId);
                await _profileRepository.UpdateAsync(profile);
            }
            catch
            {
                await _accountProvisioner.DeleteAsync(createResult.UserId);
                throw;
            }

            return PromotionResult.Success();
        }
    }
}