using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.Supporting.Currencies.ViewModels
{
    public class CurrencyVm : IMapFrom<Domain.Supporting.Currencies.Currency>
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Supporting.Currencies.Currency, CurrencyVm>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.Value))
                .ForMember(d => d.Code, opt => opt.MapFrom(s => s.Code.Value))
                .ForMember(d => d.Description, opt => opt.MapFrom(s => s.Description.Value));
        }
    }
}
