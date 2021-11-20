﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Address
{
    public class AddressVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Address>
    {
        public string Street { get; set; }
        public string BuildingNumber { get; set; }
        public int FlatNumber { get; set; }
        public int ZipCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public int CustomerId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<AddressVm, ECommerceApp.Domain.Model.Address>().ReverseMap();
        }
    }

    public class NewAddressValidation : AbstractValidator<AddressVm>
    {
        public NewAddressValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Street).NotNull();
            RuleFor(x => x.BuildingNumber).NotNull();
            RuleFor(x => x.FlatNumber).NotNull();
            RuleFor(x => x.ZipCode).NotNull();
            RuleFor(x => x.City).NotNull();
            RuleFor(x => x.Country).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}