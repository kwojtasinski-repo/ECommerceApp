namespace ECommerceApp.Application.AccountProfile.ViewModels
{
    public class AddressVm
    {
        public int Id { get; set; }
        public string Street { get; set; } = default!;
        public string BuildingNumber { get; set; } = default!;
        public int? FlatNumber { get; set; }
        public string ZipCode { get; set; } = default!;
        public string City { get; set; } = default!;
        public string Country { get; set; } = default!;

        public static AddressVm FromDomain(global::ECommerceApp.Domain.AccountProfile.Address s) => new()
        {
            Id = s.Id,
            Street = s.Street.Value,
            BuildingNumber = s.BuildingNumber.Value,
            FlatNumber = s.FlatNumber == null ? null : s.FlatNumber.Value,
            ZipCode = s.ZipCode.Value,
            City = s.City.Value,
            Country = s.Country.Value
        };
    }
}
