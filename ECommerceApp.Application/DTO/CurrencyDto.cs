using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class CurrencyDto : IMapFrom<ECommerceApp.Domain.Model.Currency>
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<CurrencyDto, ECommerceApp.Domain.Model.Currency>().ReverseMap();
        }
    }

    public class CurrencyDtoValidator : AbstractValidator<CurrencyDto>
    {
        public CurrencyDtoValidator()
        {
            RuleFor(c => c.Code).NotEmpty().Length(3);
            RuleFor(c=> c.Description).NotEmpty().MinimumLength(3).MaximumLength(255);
        }
    }
}
