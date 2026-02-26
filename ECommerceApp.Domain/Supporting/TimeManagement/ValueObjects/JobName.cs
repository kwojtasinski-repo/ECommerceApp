using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects
{
    public sealed record JobName
    {
        public string Value { get; }

        public JobName(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Job name is required.");
            if (trimmed.Length > 100)
                throw new DomainException("Job name must not exceed 100 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
