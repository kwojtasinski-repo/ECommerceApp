using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerForListVm : IMapFrom<ECommerceApp.Domain.Model.Customer>
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; } // NIP contatins 11 numbers, can be null if private person order sth
        public string CompanyName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Customer, CustomerForListVm>();
        }
    }

    public class CustomerForListValidation : AbstractValidator<CustomerForListVm>
    {
        public CustomerForListValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.FirstName).NotNull();
            RuleFor(x => x.LastName).NotNull();
            RuleFor(x => x.IsCompany).NotNull();
            RuleFor(x => x.NIP).Length(9);
            RuleFor(x => x.CompanyName).MaximumLength(100);
        }
    }
}
