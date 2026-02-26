using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.Currencies;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Currencies.Services
{
    internal sealed class CurrencyRateSyncTask : IScheduledTask
    {
        public string TaskName => "CurrencyRateSync";

        private readonly ICurrencyRateService _currencyRateService;
        private readonly ICurrencyRepository _currencyRepo;

        public CurrencyRateSyncTask(ICurrencyRateService currencyRateService, ICurrencyRepository currencyRepo)
        {
            _currencyRateService = currencyRateService;
            _currencyRepo = currencyRepo;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                var currencies = await _currencyRepo.GetAllAsync();
                var today = DateTime.UtcNow.Date;
                var synced = 0;

                foreach (var currency in currencies)
                {
                    if (currency.Id == Currency.PlnId)
                        continue;

                    await _currencyRateService.GetLatestRateAsync(currency.Id.Value);
                    synced++;
                }

                context.ReportSuccess($"Synced {synced} currency rate(s) for {today:yyyy-MM-dd}.");
            }
            catch (Exception ex)
            {
                context.ReportFailure(ex.Message);
            }
        }
    }
}
