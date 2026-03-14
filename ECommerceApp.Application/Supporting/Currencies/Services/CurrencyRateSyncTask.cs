using ECommerceApp.Application.Supporting.TimeManagement;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Currencies.Services
{
    internal sealed class CurrencyRateSyncTask : IScheduledTask
    {
        public string TaskName => "CurrencyDownloader";

        private readonly ICurrencyRateService _currencyRateService;

        public CurrencyRateSyncTask(ICurrencyRateService currencyRateService)
        {
            _currencyRateService = currencyRateService;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var synced = await _currencyRateService.SyncAllRatesAsync(cancellationToken);
                context.ReportSuccess($"Synced {synced} currency rate(s) for {today:yyyy-MM-dd}.");
            }
            catch (Exception ex)
            {
                context.ReportFailure(ex.Message);
            }
        }
    }
}
