﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class CustomerDto : IMapFrom<CustomerDto>
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; } // NIP contatins 11 numbers, can be null if private person order sth
        public string CompanyName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Customer, CustomerDto>()
                .ForMember(c => c.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(c => c.FirstName, opt => opt.MapFrom(src => src.FirstName))
                .ForMember(c => c.LastName, opt => opt.MapFrom(src => src.LastName))
                .ForMember(c => c.IsCompany, opt => opt.MapFrom(src => src.IsCompany))
                .ForMember(c => c.NIP, opt => opt.MapFrom(src => src.NIP))
                .ForMember(c => c.CompanyName, opt => opt.MapFrom(src => src.CompanyName))
                .ForMember(c => c.UserId, opt => opt.MapFrom(src => src.UserId))
                .ReverseMap();
        }
    }


    public class CustomerDtoValidator : AbstractValidator<CustomerDto>
    {
        public CustomerDtoValidator()
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
