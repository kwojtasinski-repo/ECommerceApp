namespace ECommerceApp.Domain.Shared
{
    public sealed record Money
    {
        public decimal Amount { get; }
        public string CurrencyCode { get; }
        public decimal Rate { get; }

        public Money(decimal amount, string currencyCode, decimal rate)
        {
            if (amount <= 0)
                throw new DomainException("Amount must be positive.");
            if (string.IsNullOrWhiteSpace(currencyCode))
                throw new DomainException("Currency code is required.");
            if (rate <= 0)
                throw new DomainException("Rate must be positive.");
            Amount = amount;
            CurrencyCode = currencyCode.ToUpperInvariant().Trim();
            Rate = rate;
        }

        public static Money Pln(decimal amount) => new Money(amount, "PLN", 1m);

        public decimal ToBaseCurrency() => Amount * Rate;

        public override string ToString() => $"{Amount:F2} {CurrencyCode}";
    }
}
