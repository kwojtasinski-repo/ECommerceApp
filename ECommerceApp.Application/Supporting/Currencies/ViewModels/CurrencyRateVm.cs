using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;

namespace ECommerceApp.Application.Supporting.Currencies.ViewModels
{
    public class CurrencyRateVm : IMapFrom<Domain.Supporting.Currencies.CurrencyRate>
    {
        public int Id { get; set; }
        public int CurrencyId { get; set; }
        public decimal Rate { get; set; }
        public DateTime CurrencyDate { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Supporting.Currencies.CurrencyRate, CurrencyRateVm>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.Value))
                .ForMember(d => d.CurrencyId, opt => opt.MapFrom(s => s.CurrencyId.Value));
        }
    }
}
