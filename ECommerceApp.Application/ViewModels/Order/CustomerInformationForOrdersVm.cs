using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class CustomerInformationForOrdersVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Customer>
    {
        public string Information { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Customer, CustomerInformationForOrdersVm>()
                .ForMember(i => i.Information, opt => opt.MapFrom(c => (c.NIP != null && c.CompanyName != null) ?
                            c.FirstName + " " + c.LastName + " " + c.NIP + " " + c.CompanyName
                            : c.FirstName + " " + c.LastName));                
        }
    }
}
