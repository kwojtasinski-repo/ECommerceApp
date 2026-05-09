using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public sealed class OrderCustomer
    {
        public string FirstName { get; private set; } = default!;
        public string LastName { get; private set; } = default!;
        public string Email { get; private set; } = default!;
        public string PhoneNumber { get; private set; } = default!;
        public bool IsCompany { get; private set; }
        public string CompanyName { get; private set; }
        public string Nip { get; private set; }
        public string Street { get; private set; } = default!;
        public string BuildingNumber { get; private set; } = default!;
        public string FlatNumber { get; private set; }
        public string ZipCode { get; private set; } = default!;
        public string City { get; private set; } = default!;
        public string Country { get; private set; } = default!;

        private OrderCustomer() { }

        public OrderCustomer(
            string firstName,
            string lastName,
            string email,
            string phoneNumber,
            bool isCompany,
            string companyName,
            string nip,
            string street,
            string buildingNumber,
            string flatNumber,
            string zipCode,
            string city,
            string country)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw new DomainException("OrderCustomer.FirstName is required.");
            if (string.IsNullOrWhiteSpace(lastName))
                throw new DomainException("OrderCustomer.LastName is required.");
            if (string.IsNullOrWhiteSpace(street))
                throw new DomainException("OrderCustomer.Street is required.");
            if (string.IsNullOrWhiteSpace(buildingNumber))
                throw new DomainException("OrderCustomer.BuildingNumber is required.");
            if (string.IsNullOrWhiteSpace(zipCode))
                throw new DomainException("OrderCustomer.ZipCode is required.");
            if (string.IsNullOrWhiteSpace(city))
                throw new DomainException("OrderCustomer.City is required.");
            if (string.IsNullOrWhiteSpace(country))
                throw new DomainException("OrderCustomer.Country is required.");

            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException("OrderCustomer.Email is required.");
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new DomainException("OrderCustomer.PhoneNumber is required.");

            FirstName = firstName;
            LastName = lastName;
            Email = email;
            PhoneNumber = phoneNumber;
            IsCompany = isCompany;
            CompanyName = companyName;
            Nip = nip;
            Street = street;
            BuildingNumber = buildingNumber;
            FlatNumber = flatNumber;
            ZipCode = zipCode;
            City = city;
            Country = country;
        }
    }
}
