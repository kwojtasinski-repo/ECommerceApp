using ECommerceApp.Application.AccountProfile.Options;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.AccountProfile;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Services
{
    internal sealed class UnclaimedGuestProfileCleanupTask : IScheduledTask
    {
        public string TaskName => "UnclaimedGuestProfileCleanup";

        private readonly IUserProfileRepository _profiles;
        private readonly IGuestAccountProvisioner _accountProvisioner;
        private readonly IModuleClient _moduleClient;
        private readonly IOptions<GuestProfileCleanupOptions> _options;

        public UnclaimedGuestProfileCleanupTask(
            IUserProfileRepository profiles,
            IGuestAccountProvisioner accountProvisioner,
            IModuleClient moduleClient,
            IOptions<GuestProfileCleanupOptions> options)
        {
            _profiles = profiles;
            _accountProvisioner = accountProvisioner;
            _moduleClient = moduleClient;
            _options = options;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                var options = _options.Value;
                if (!options.Enabled)
                {
                    context.ReportSuccess("Disabled via configuration; no profiles evaluated.");
                    return;
                }

                var cutoff = DateTime.UtcNow.AddDays(-options.RetentionDays);
                var candidates = await _profiles.GetOlderThanAsync(cutoff);
                if (candidates.Count == 0)
                {
                    context.ReportSuccess("No candidate profiles older than the retention threshold.");
                    return;
                }

                // Guest-ness is derived, never stored (ADR-0030 §4): a candidate is unclaimed when its
                // UserId has no matching ApplicationUser. Batched to avoid N+1 lookups.
                var registeredUserIds = await _accountProvisioner.GetRegisteredUserIdsAsync(
                    candidates.Select(p => p.UserId).ToList());
                var unclaimed = candidates.Where(p => !registeredUserIds.Contains(p.UserId)).ToList();
                if (unclaimed.Count == 0)
                {
                    context.ReportSuccess("No unclaimed candidate profiles found.");
                    return;
                }

                // Critical invariant: never delete a profile that has any Order, claimed or not.
                var customerIdsWithOrders = await _moduleClient.SendAsync(
                    new CustomersWithOrdersQuery(unclaimed.Select(p => p.Id.Value).ToList()),
                    cancellationToken);
                var deletable = unclaimed.Where(p => !customerIdsWithOrders.Contains(p.Id.Value)).ToList();

                var deletedCount = 0;
                foreach (var profile in deletable)
                {
                    if (await _profiles.DeleteAsync(profile.Id))
                    {
                        deletedCount++;
                    }
                }

                context.ReportSuccess($"Deleted {deletedCount} unclaimed guest profile(s) older than {options.RetentionDays} day(s).");
            }
            catch (Exception ex)
            {
                context.ReportFailure(ex.Message);
            }
        }
    }
}
