using ECommerceApp.Domain.AccountProfile.ValueObjects;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Domain.AccountProfile
{
    public class UserProfile
    {
        public UserProfileId Id { get; private set; }
        public string UserId { get; private set; } = default!;
        public string FirstName { get; private set; } = default!;
        public string LastName { get; private set; } = default!;
        public bool IsCompany { get; private set; }
        public Nip? NIP { get; private set; }
        public CompanyName? CompanyName { get; private set; }
        public Email Email { get; private set; } = default!;
        public PhoneNumber PhoneNumber { get; private set; } = default!;

        private readonly List<Address> _addresses = new();
        public IReadOnlyList<Address> Addresses => _addresses.AsReadOnly();

        private UserProfile() { }

        public static (UserProfile Profile, UserProfileCreated Event) Create(
            string userId,
            string firstName,
            string lastName,
            bool isCompany,
            string? nip,
            string? companyName,
            string email,
            string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId cannot be empty", nameof(userId));
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("FirstName cannot be empty", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("LastName cannot be empty", nameof(lastName));
            if (isCompany && string.IsNullOrWhiteSpace(companyName))
                throw new ArgumentException("CompanyName is required for a company profile", nameof(companyName));
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty", nameof(email));
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("PhoneNumber cannot be empty", nameof(phoneNumber));

            var profile = new UserProfile
            {
                UserId = userId,
                FirstName = firstName,
                LastName = lastName,
                IsCompany = isCompany,
                NIP = nip != null ? new Nip(nip) : null,
                CompanyName = companyName != null ? new CompanyName(companyName) : null,
                Email = new Email(email),
                PhoneNumber = new PhoneNumber(phoneNumber)
            };

            var @event = new UserProfileCreated(profile.Id.Value, userId, DateTime.UtcNow);
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
            NIP = nip != null ? new Nip(nip) : null;
            CompanyName = companyName != null ? new CompanyName(companyName) : null;
        }

        public void UpdateContactInfo(string email, string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty", nameof(email));
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("PhoneNumber cannot be empty", nameof(phoneNumber));

            Email = new Email(email);
            PhoneNumber = new PhoneNumber(phoneNumber);
        }

        public void AddAddress(
            string street,
            string buildingNumber,
            int? flatNumber,
            string zipCode,
            string city,
            string country)
        {
            _addresses.Add(new Address(street, buildingNumber, flatNumber, zipCode, city, country));
        }

        public bool UpdateAddress(
            int addressId,
            string street,
            string buildingNumber,
            int? flatNumber,
            string zipCode,
            string city,
            string country)
        {
            var idx = _addresses.FindIndex(a => a.Id == new AddressId(addressId));
            if (idx < 0)
                return false;

            _addresses[idx] = new Address(new AddressId(addressId), street, buildingNumber, flatNumber, zipCode, city, country);
            return true;
        }

        public bool RemoveAddress(int addressId)
        {
            var idx = _addresses.FindIndex(a => a.Id == new AddressId(addressId));
            if (idx < 0)
                return false;

            _addresses.RemoveAt(idx);
            return true;
        }
    }
}
