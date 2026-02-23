using System;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Profiles.AccountProfile
{
    public class AccountProfile
    {
        public int Id { get; private set; }
        public string UserId { get; private set; } = default!;
        public string FirstName { get; private set; } = default!;
        public string LastName { get; private set; } = default!;
        public bool IsCompany { get; private set; }
        public string? NIP { get; private set; }
        public string? CompanyName { get; private set; }

        private readonly List<Address> _addresses = new();
        public IReadOnlyList<Address> Addresses => _addresses.AsReadOnly();

        private readonly List<ContactDetail> _contactDetails = new();
        public IReadOnlyList<ContactDetail> ContactDetails => _contactDetails.AsReadOnly();

        private AccountProfile() { }

        public static (AccountProfile Profile, AccountProfileCreated Event) Create(
            string userId,
            string firstName,
            string lastName,
            bool isCompany,
            string? nip,
            string? companyName)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId cannot be empty", nameof(userId));
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("FirstName cannot be empty", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("LastName cannot be empty", nameof(lastName));
            if (isCompany && string.IsNullOrWhiteSpace(companyName))
                throw new ArgumentException("CompanyName is required for a company profile", nameof(companyName));

            var profile = new AccountProfile
            {
                UserId = userId,
                FirstName = firstName,
                LastName = lastName,
                IsCompany = isCompany,
                NIP = nip,
                CompanyName = companyName
            };

            var @event = new AccountProfileCreated(profile.Id, userId, DateTime.UtcNow);
            return (profile, @event);
        }

        public void UpdatePersonalInfo(
            string firstName,
            string lastName,
            bool isCompany,
            string? nip,
            string? companyName)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("FirstName cannot be empty", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("LastName cannot be empty", nameof(lastName));
            if (isCompany && string.IsNullOrWhiteSpace(companyName))
                throw new ArgumentException("CompanyName is required for a company profile", nameof(companyName));

            FirstName = firstName;
            LastName = lastName;
            IsCompany = isCompany;
            NIP = nip;
            CompanyName = companyName;
        }
    }
}
