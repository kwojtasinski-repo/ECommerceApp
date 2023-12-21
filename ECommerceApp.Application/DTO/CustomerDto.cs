using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.DTO
{
    public class CustomerDto : IMapFrom<CustomerDto>
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; }
        public string CompanyName { get; set; }

        public List<ContactDetailDto> ContactDetails { get; set; } = new();
        public List<ContactDetailTypeDto> ContactDetailTypes { get; set; } = new();
        public virtual List<AddressDto> Addresses { get; set; } = new();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<CustomerDto, Domain.Model.Customer>().ReverseMap()
                .ForMember(p => p.ContactDetails, opt => opt.MapFrom(ps => ps.ContactDetails))
                .ForMember(p => p.Addresses, opt => opt.MapFrom(ps => ps.Addresses));
        }
    }

    public class CustomerDtoValidation : AbstractValidator<CustomerDto>
    {
        public CustomerDtoValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.FirstName).NotNull().MinimumLength(3).MaximumLength(255);
            RuleFor(x => x.LastName).NotNull().MinimumLength(3).MaximumLength(255);
            RuleFor(x => x.IsCompany).NotNull();
            When(x => x.IsCompany, () =>
            {
                RuleFor(x => x.NIP).NotNull().Length(9);
                RuleFor(x => x.CompanyName).NotNull().MinimumLength(3).MaximumLength(255);
            });
        }
    }
}
