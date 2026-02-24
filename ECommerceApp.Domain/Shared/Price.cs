namespace ECommerceApp.Domain.Shared
{
    public sealed record Price
    {
        public decimal Amount { get; }

        public Price(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException("Price must be positive.");
            Amount = amount;
        }

        public Money ToMoney(decimal rate = 1m) => new Money(Amount, "PLN", rate);

        public override string ToString() => Amount.ToString("F2");
    }
}
