using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.AccountProfile.ViewModels
{
    public class AddressVm : IMapFrom<global::ECommerceApp.Domain.AccountProfile.Address>
    {
        public int Id { get; set; }
        public string Street { get; set; } = default!;
        public string BuildingNumber { get; set; } = default!;
        public int? FlatNumber { get; set; }
        public int ZipCode { get; set; }
        public string City { get; set; } = default!;
        public string Country { get; set; } = default!;

        public void Mapping(Profile profile)
        {
            profile.CreateMap<global::ECommerceApp.Domain.AccountProfile.Address, AddressVm>();
        }
    }
}
