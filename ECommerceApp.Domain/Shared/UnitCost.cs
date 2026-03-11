namespace ECommerceApp.Domain.Shared
{
    public sealed record UnitCost
    {
        public decimal Amount { get; }

        public UnitCost(decimal amount)
        {
            if (amount < 0)
            {
                throw new DomainException("UnitCost cannot be negative.");
            }

            Amount = amount;
        }

        public static UnitCost Zero => new(0m);

        public override string ToString() => Amount.ToString("F2");
    }
}
