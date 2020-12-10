﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class NewItemBrandVm : IMapFrom<ECommerceApp.Domain.Model.Brand>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewItemBrandVm, ECommerceApp.Domain.Model.Brand>().ReverseMap();
        }
    }
}
