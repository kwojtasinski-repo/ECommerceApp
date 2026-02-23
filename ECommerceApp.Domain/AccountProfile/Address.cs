using System;

namespace ECommerceApp.Domain.AccountProfile
{
    public class Address
    {
        public int Id { get; private set; }
        public string Street { get; private set; } = default!;
        public string BuildingNumber { get; private set; } = default!;
        public int? FlatNumber { get; private set; }
        public int ZipCode { get; private set; }
        public string City { get; private set; } = default!;
        public string Country { get; private set; } = default!;

        private Address() { }

        internal static Address Create(
            string street,
            string buildingNumber,
            int? flatNumber,
            int zipCode,
            string city,
            string country)
        {
            if (string.IsNullOrWhiteSpace(street))
                throw new ArgumentException("Street cannot be empty", nameof(street));
            if (string.IsNullOrWhiteSpace(buildingNumber))
                throw new ArgumentException("BuildingNumber cannot be empty", nameof(buildingNumber));
            if (zipCode <= 0)
                throw new ArgumentException("ZipCode must be positive", nameof(zipCode));
            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City cannot be empty", nameof(city));
            if (string.IsNullOrWhiteSpace(country))
                throw new ArgumentException("Country cannot be empty", nameof(country));

            return new Address
            {
                Street = street,
                BuildingNumber = buildingNumber,
                FlatNumber = flatNumber,
                ZipCode = zipCode,
                City = city,
                Country = country
            };
        }

        internal void Update(
            string street,
            string buildingNumber,
            int? flatNumber,
            int zipCode,
            string city,
            string country)
        {
            if (string.IsNullOrWhiteSpace(street))
                throw new ArgumentException("Street cannot be empty", nameof(street));
            if (string.IsNullOrWhiteSpace(buildingNumber))
                throw new ArgumentException("BuildingNumber cannot be empty", nameof(buildingNumber));
            if (zipCode <= 0)
                throw new ArgumentException("ZipCode must be positive", nameof(zipCode));
            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City cannot be empty", nameof(city));
            if (string.IsNullOrWhiteSpace(country))
                throw new ArgumentException("Country cannot be empty", nameof(country));

            Street = street;
            BuildingNumber = buildingNumber;
            FlatNumber = flatNumber;
            ZipCode = zipCode;
            City = city;
            Country = country;
        }
    }
}
