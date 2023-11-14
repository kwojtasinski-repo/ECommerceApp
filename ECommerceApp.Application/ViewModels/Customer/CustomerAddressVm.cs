using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Address;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerAddressVm : BaseVm, IMapFrom<AddressVm>
    {
        public string Street { get; set; }
        public string BuildingNumber { get; set; }
        public int? FlatNumber { get; set; }
        public int ZipCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public int CustomerId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<AddressVm, CustomerAddressVm>()
                .ForMember(c => c.Id, opt => opt.MapFrom(a => a.Id))
                .ForMember(c => c.BuildingNumber, opt => opt.MapFrom(a => a.Address.BuildingNumber))
                .ForMember(c => c.FlatNumber, opt => opt.MapFrom(a => a.Address.FlatNumber))
                .ForMember(c => c.ZipCode, opt => opt.MapFrom(a => a.Address.ZipCode))
                .ForMember(c => c.City, opt => opt.MapFrom(a => a.Address.City))
                .ForMember(c => c.Country, opt => opt.MapFrom(a => a.Address.Country))
                .ForMember(c => c.CustomerId, opt => opt.MapFrom(a => a.Address.CustomerId));
        }
    }
}
