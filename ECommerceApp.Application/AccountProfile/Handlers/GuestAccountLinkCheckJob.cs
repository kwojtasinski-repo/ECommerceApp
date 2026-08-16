using ECommerceApp.Application.AccountProfile.Contracts;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.AccountProfile;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Handlers
{
    public sealed class GuestAccountLinkCheckJob : IScheduledTask
    {
        public const string JobTaskName = "GuestAccountLinkCheckJob";

        public string TaskName => JobTaskName;

        private readonly IUserProfileRepository _profileRepository;
        private readonly IGuestAccountProvisioner _accountProvisioner;
        private readonly IVerificationCodeClient _verificationCodeClient;

        public GuestAccountLinkCheckJob(
            IUserProfileRepository profileRepository,
            IGuestAccountProvisioner accountProvisioner,
            IVerificationCodeClient verificationCodeClient)
        {
            _profileRepository = profileRepository;
            _accountProvisioner = accountProvisioner;
            _verificationCodeClient = verificationCodeClient;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(context.EntityId))
                {
                    context.ReportFailure("Missing registration email.");
                    return;
                }

                var candidates = await _profileRepository.GetByEmailAsync(context.EntityId);
                if (candidates.Count == 0)
                {
                    context.ReportSuccess("No profiles matched the registration email.");
                    return;
                }

                var registeredUserIds = await _accountProvisioner.GetRegisteredUserIdsAsync(
                    candidates.Select(profile => profile.UserId).ToList());
                var unclaimed = candidates
                    .Where(profile => !registeredUserIds.Contains(profile.UserId))
                    .ToList();

                if (unclaimed.Count == 0)
                {
                    context.ReportSuccess("No unclaimed profiles matched the registration email.");
                    return;
                }

                await _verificationCodeClient.RequestGuestAccountLinkAsync(context.EntityId, cancellationToken);
                context.ReportSuccess($"Guest account link requested for {unclaimed.Count} profile(s).");
            }
            catch (Exception ex)
            {
                context.ReportFailure(ex.Message);
            }
        }
    }
}
