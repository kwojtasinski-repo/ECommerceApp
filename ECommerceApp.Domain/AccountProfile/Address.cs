using ECommerceApp.Domain.AccountProfile.ValueObjects;

namespace ECommerceApp.Domain.AccountProfile
{
    public sealed record Address
    {
        public AddressId Id { get; private set; } = new AddressId(0);
        public Street Street { get; }
        public BuildingNumber BuildingNumber { get; }
        public FlatNumber? FlatNumber { get; }
        public ZipCode ZipCode { get; }
        public City City { get; }
        public Country Country { get; }

        // Used by EF Core constructor injection (parameters match property names/types after value converters)
        private Address(AddressId id, Street street, BuildingNumber buildingNumber, FlatNumber? flatNumber, ZipCode zipCode, City city, Country country)
        {
            Id = id;
            Street = street;
            BuildingNumber = buildingNumber;
            FlatNumber = flatNumber;
            ZipCode = zipCode;
            City = city;
            Country = country;
        }

        // Used by aggregate UpdateAddress to preserve AddressId — EF Core issues UPDATE not DELETE+INSERT
        internal Address(AddressId id, string street, string buildingNumber, int? flatNumber, string zipCode, string city, string country)
            : this(street, buildingNumber, flatNumber, zipCode, city, country)
        {
            Id = id;
        }

        public Address(string street, string buildingNumber, int? flatNumber, string zipCode, string city, string country)
        {
            Street = new Street(street);
            BuildingNumber = new BuildingNumber(buildingNumber);
            FlatNumber = flatNumber.HasValue ? new FlatNumber(flatNumber.Value) : null;
            ZipCode = new ZipCode(zipCode);
            City = new City(city);
            Country = new Country(country);
        }
    }
}
