using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.AccountProfile
{
    public class UserProfile
    {
        public int Id { get; private set; }
        public string UserId { get; private set; } = default!;
        public string FirstName { get; private set; } = default!;
        public string LastName { get; private set; } = default!;
        public bool IsCompany { get; private set; }
        public string? NIP { get; private set; }
        public string? CompanyName { get; private set; }
        public string Email { get; private set; } = default!;
        public string PhoneNumber { get; private set; } = default!;

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
                NIP = nip,
                CompanyName = companyName,
                Email = email,
                PhoneNumber = phoneNumber
            };

            var @event = new UserProfileCreated(profile.Id, userId, DateTime.UtcNow);
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

        public void UpdateContactInfo(string email, string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty", nameof(email));
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("PhoneNumber cannot be empty", nameof(phoneNumber));

            Email = email;
            PhoneNumber = phoneNumber;
        }

        public void AddAddress(
            string street,
            string buildingNumber,
            int? flatNumber,
            int zipCode,
            string city,
            string country)
        {
            var address = Address.Create(street, buildingNumber, flatNumber, zipCode, city, country);
            _addresses.Add(address);
        }

        public bool UpdateAddress(
            int addressId,
            string street,
            string buildingNumber,
            int? flatNumber,
            int zipCode,
            string city,
            string country)
        {
            var address = _addresses.FirstOrDefault(a => a.Id == addressId);
            if (address is null)
                return false;

            address.Update(street, buildingNumber, flatNumber, zipCode, city, country);
            return true;
        }

        public bool RemoveAddress(int addressId)
        {
            var address = _addresses.FirstOrDefault(a => a.Id == addressId);
            if (address is null)
                return false;

            _addresses.Remove(address);
            return true;
        }
    }
}
