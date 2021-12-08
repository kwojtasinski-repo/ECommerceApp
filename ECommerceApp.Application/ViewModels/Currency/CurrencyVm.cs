using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Currency
{
    public class CurrencyVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Currency>
    {
        public string Code { get; set; }
        public string Description { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<CurrencyVm, ECommerceApp.Domain.Model.Currency>().ReverseMap();
        }
    }
}
