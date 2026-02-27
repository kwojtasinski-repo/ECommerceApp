using ECommerceApp.Domain.Supporting.Currencies.ValueObjects;

namespace ECommerceApp.Domain.Supporting.Currencies
{
    public class Currency
    {
        public static readonly CurrencyId PlnId = new CurrencyId(1);

        public CurrencyId Id { get; private set; }
        public CurrencyCode Code { get; private set; } = default!;
        public CurrencyDescription Description { get; private set; } = default!;

        private Currency() { }

        public static Currency Create(string code, string description)
        {
            return new Currency
            {
                Code = new CurrencyCode(code),
                Description = new CurrencyDescription(description)
            };
        }

        public void Update(string code, string description)
        {
            Code = new CurrencyCode(code);
            Description = new CurrencyDescription(description);
        }
    }
}
