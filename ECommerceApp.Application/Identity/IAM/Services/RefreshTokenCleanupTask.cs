using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Identity.IAM;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Identity.IAM.Services
{
    internal sealed class RefreshTokenCleanupTask : IScheduledTask
    {
        public string TaskName => "RefreshTokenCleanup";

        private readonly IRefreshTokenRepository _refreshTokens;

        public RefreshTokenCleanupTask(IRefreshTokenRepository refreshTokens)
        {
            _refreshTokens = refreshTokens;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _refreshTokens.DeleteExpiredAsync(cancellationToken);
                context.ReportSuccess($"Deleted {deleted} expired refresh token(s).");
            }
            catch (Exception ex)
            {
                context.ReportFailure(ex.Message);
            }
        }
    }
}
