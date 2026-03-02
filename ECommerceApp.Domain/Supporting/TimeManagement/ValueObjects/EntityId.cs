using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects
{
    public sealed record EntityId
    {
        public string Value { get; }

        public EntityId(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Entity ID is required.");
            if (trimmed.Length > 200)
                throw new DomainException("Entity ID must not exceed 200 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
