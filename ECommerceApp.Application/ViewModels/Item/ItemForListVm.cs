﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Item>
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Item, ItemForListVm>();
        }
    }

    public class ItemForListValidation : AbstractValidator<ItemForListVm>
    {
        public ItemForListValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
            RuleFor(x => x.Cost).NotNull();
            RuleFor(x => x.Description).NotNull();
            RuleFor(x => x.Warranty).NotNull();
            RuleFor(x => x.Quantity).NotNull();
            RuleFor(x => x.BrandId).NotNull();
            RuleFor(x => x.TypeId).NotNull();
        }
    }
}
