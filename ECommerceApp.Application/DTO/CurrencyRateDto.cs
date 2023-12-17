using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels;
using FluentValidation;
using System;

namespace ECommerceApp.Application.DTO
{

    public class CurrencyRateDto : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CurrencyRate>
    {
        public int CurrencyId { get; set; }
        public decimal Rate { get; set; }
        public DateTime CurrencyDate { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<CurrencyRateDto, ECommerceApp.Domain.Model.CurrencyRate>().ReverseMap();
        }
    }

    public class CurrencyRateDtoValidator : AbstractValidator<CurrencyRateDto>
    {
        public CurrencyRateDtoValidator()
        {
            RuleFor(c => c.Rate).NotEmpty().GreaterThan(0);
            RuleFor(c => c.CurrencyDate).NotEmpty().NotNull().GreaterThan(new DateTime());
        }
    }
}
