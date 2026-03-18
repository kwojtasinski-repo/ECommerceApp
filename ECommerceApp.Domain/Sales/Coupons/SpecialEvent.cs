using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public sealed record SpecialEventId(int Value) : TypedId<int>(Value);

    public class SpecialEvent
    {
        public SpecialEventId Id { get; private set; }
        public string Code { get; private set; }
        public string Name { get; private set; }
        public DateTime StartsAt { get; private set; }
        public DateTime EndsAt { get; private set; }
        public bool IsActive { get; private set; }

        private SpecialEvent() { }

        public static SpecialEvent Create(string code, string name, DateTime startsAt, DateTime endsAt)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException("Event code is required.");
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Event name is required.");
            if (endsAt <= startsAt)
                throw new DomainException("EndsAt must be after StartsAt.");

            return new SpecialEvent
            {
                Code = code,
                Name = name,
                StartsAt = startsAt,
                EndsAt = endsAt,
                IsActive = true
            };
        }

        public bool IsCurrentlyActive(DateTime utcNow)
            => IsActive && utcNow >= StartsAt && utcNow <= EndsAt;

        public void Deactivate() => IsActive = false;

        public void Activate() => IsActive = true;
    }
}
