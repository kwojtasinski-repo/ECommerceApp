using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.CurrencyRate
{
    public class CurrencyRateVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CurrencyRate>
    {
        public int CurrencyId { get; set; }
        public decimal Rate { get; set; }
        public DateTime CurrencyDate { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<CurrencyRateVm, ECommerceApp.Domain.Model.CurrencyRate>().ReverseMap();
        }
    }
}
