using System;

namespace ECommerceApp.Application.Supporting.Currencies.ViewModels
{
    public class CurrencyRateVm
    {
        public int Id { get; set; }
        public int CurrencyId { get; set; }
        public decimal Rate { get; set; }
        public DateTime CurrencyDate { get; set; }

        public static CurrencyRateVm FromDomain(Domain.Supporting.Currencies.CurrencyRate s) => new()
        {
            Id = s.Id?.Value ?? 0,
            CurrencyId = s.CurrencyId?.Value ?? 0,
            Rate = s.Rate,
            CurrencyDate = s.CurrencyDate
        };
    }
}
