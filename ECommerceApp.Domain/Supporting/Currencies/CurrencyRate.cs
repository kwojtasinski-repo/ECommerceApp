using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Supporting.Currencies
{
    public class CurrencyRate
    {
        public CurrencyRateId Id { get; private set; } = new CurrencyRateId(0);
        public CurrencyId CurrencyId { get; private set; } = default!;
        public decimal Rate { get; private set; }
        public DateTime CurrencyDate { get; private set; }

        private CurrencyRate() { }

        public static CurrencyRate Create(CurrencyId currencyId, decimal rate, DateTime currencyDate)
        {
            if (currencyId is null)
                throw new DomainException("Currency id is required.");
            if (rate <= 0)
                throw new DomainException("Rate must be positive.");
            return new CurrencyRate
            {
                CurrencyId = currencyId,
                Rate = rate,
                CurrencyDate = currencyDate.Date
            };
        }
    }
}
